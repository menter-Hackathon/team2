using UnityEngine;

public class FeedbackEffectManager : MonoBehaviour
{
    [Header("パーティクル")]
    public ParticleSystem successVFX;
    public ParticleSystem missVFX;

    void Update()
    {
        // Aキーを押した瞬間にキラキラ（正解）を再生
        if (Input.GetKeyDown(KeyCode.A))
        {
            PlaySuccess();
        }

        // Sキーを押した瞬間にどよーん（不正解）を再生
        if (Input.GetKeyDown(KeyCode.S))
        {
            PlayMiss();
        }
    }

    // 正解した時に呼ぶメソッド
    public void PlaySuccess()
    {
        if (successVFX != null)
        {
            successVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            successVFX.Play();
        }
    }

    // 間違えた時に呼ぶメソッド
    public void PlayMiss()
    {
        if (missVFX != null)
        {
            missVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            missVFX.Play();
        }
    }
}