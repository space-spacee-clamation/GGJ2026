using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    public void StartGameScene(){
        canvasGroup.DOFade(1, 0.6f).OnComplete(() =>
        {
            SceneManager.LoadScene("Mian");
        });
    }
    void Start()
    {
        AudioManager.I.Play(AudioKey.Menu_BGM);
    }
}
