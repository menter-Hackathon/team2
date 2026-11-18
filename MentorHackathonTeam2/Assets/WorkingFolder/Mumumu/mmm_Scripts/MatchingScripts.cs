 using Photon.Pun;
 using Photon.Realtime;
 using UnityEngine;
 using ExitGames.Client.Photon;

 public class MatchingScripts : MonoBehaviourPunCallbacks
 {
    private void Start() 
    {
        Debug.Log("Start実行");

        PhotonNetwork.AutomaticallySyncScene = true;

        PhotonNetwork.NickName = "Player";
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster() 
    {
        Debug.Log("マスターサーバーに接続成功");
        // ランダムなルームに参加する
        PhotonNetwork.JoinRandomRoom();
    }

    // ランダムで参加できるルームが存在しないなら、新規でルームを作成する
    public override void OnJoinRandomFailed(short returnCode, string message) 
    {
        // ルームの参加人数を2人に設定する
        var roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = 2;

        PhotonNetwork.CreateRoom(null, roomOptions);
    }

    public override void OnJoinedRoom() 
    {
        Debug.Log("ルームに参加しました");
        Debug.Log("ルーム名：" + PhotonNetwork.CurrentRoom.Name);


        if (PhotonNetwork.IsMasterClient) 
        {
            PhotonNetwork.CurrentRoom.SetStartTime(PhotonNetwork.ServerTimestamp);
        }

        // ルームが満員になったら、以降そのルームへの参加を不許可にする
        if(PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // ルームの人数が２人になったか確認
        if(PhotonNetwork.CurrentRoom.PlayerCount ==2)
        {
            // マスタークライアントだけがシーンを移動する
            if(PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.LoadLevel("mmm_Day1");
            }
        }
    }


}