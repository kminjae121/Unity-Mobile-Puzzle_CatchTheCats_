using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace _Code.Manager
{
    public class MainScene : MonoBehaviour
    {
        [SerializeField] private RectTransform image;
            
        public void StartDotween()
        {
            image.DOScale(new Vector3(1, 1, 1), 0.6f);
        }
    }
}