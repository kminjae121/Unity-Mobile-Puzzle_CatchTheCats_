using System;
using System.Collections;
using System.Collections.Generic;
using Code.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScene : MonoBehaviour
{
    [SerializeField] private Image barImg;
    [SerializeField] private float loadingSec;
    [SerializeField] private TextMeshProUGUI loadingTxt;
    [SerializeField] private List<string> loadingStrs;

    public UnityEvent EndEvent;
    
    private bool isLoading;
    private bool progressFinished;
    private bool contentReady;
    
    private void Awake()
    {
        StartLoading();
    }

    private void StartLoading()
    {
        isLoading = true;
        progressFinished = false;
        contentReady = false;
        barImg.DOKill();
        barImg.fillAmount = 0f;
        StartCoroutine(LoadingTxt());
        
        barImg.DOFillAmount(1, loadingSec).SetEase(Ease.InOutCirc)
            .OnComplete(() =>
            {
                progressFinished = true;
                gameObject.SetActive(false);
            });
    }

    public void CompleteLoading()
    {
        contentReady = true;
        TryFinishLoading();
    }

    private void TryFinishLoading()
    {
        if (!progressFinished || !contentReady)
            return;

        isLoading = false;
        gameObject.SetActive(false);
        EndEvent?.Invoke();
    }


    private IEnumerator LoadingTxt()
    {
        int idx = 0;
        
        while (isLoading)
        {
            if (idx > loadingStrs.Count - 1)
                idx = 0;
            
            yield return new WaitForSeconds(0.4f);

            loadingTxt.text = loadingStrs[idx];

            idx += 1;
        }
    }
}
