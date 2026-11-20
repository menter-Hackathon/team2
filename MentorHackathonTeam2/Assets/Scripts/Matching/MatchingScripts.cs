using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>2人用ルームを探し、揃ったらオンラインゲームへ進む。</summary>
public class MatchingScripts : MonoBehaviourPunCallbacks
{
    private const string TitleScene = "TitleSelect";
    private const string OnlineScene = "Online_Day1";
    private bool _isCancelling;
    private bool _isJoining;

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.NickName = PlayerPrefs.GetString("TitleSelect.Nickname", "Player");

        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else if (PhotonNetwork.IsConnectedAndReady)
            JoinRandomRoom();
        else
            PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        if (!_isCancelling)
            JoinRandomRoom();
    }

    public override void OnLeftRoom()
    {
        if (!_isCancelling)
            JoinRandomRoom();
    }

    private void JoinRandomRoom()
    {
        if (_isCancelling || _isJoining || !PhotonNetwork.IsConnectedAndReady || PhotonNetwork.InRoom)
            return;

        _isJoining = PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (_isCancelling) return;

        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 2 });
    }

    public override void OnJoinedRoom()
    {
        if (_isCancelling) return;
        _isJoining = false;

        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.CurrentRoom.SetStartTime(PhotonNetwork.ServerTimestamp);

        if (PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
            PhotonNetwork.CurrentRoom.IsOpen = false;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (_isCancelling || PhotonNetwork.CurrentRoom.PlayerCount != 2 || !PhotonNetwork.IsMasterClient)
            return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.LoadLevel(OnlineScene);
    }

    /// <summary>通信を終了してタイトルへ戻る。遅れて届いたPhotonイベントも無視する。</summary>
    public void CancelMatching()
    {
        if (_isCancelling) return;

        _isCancelling = true;
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.Disconnect();
        SceneManager.LoadScene(TitleScene);
    }
}
