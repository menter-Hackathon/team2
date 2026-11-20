using UnityEngine;
using Photon.Pun;

public class OnlineGameController : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("ゲームシーン開始");

        Vector3 position;

        // マスタークライアントと参加者で位置を分ける
        if (PhotonNetwork.IsMasterClient)
        {
            position = new Vector3(-2f, 0f, 0f);
        }
        else
        {
            position = new Vector3(2f, 0f, 0f);
        }

        // 自分のAvatarを生成する
        PhotonNetwork.Instantiate("Avatar", position, Quaternion.identity);
    }
}
