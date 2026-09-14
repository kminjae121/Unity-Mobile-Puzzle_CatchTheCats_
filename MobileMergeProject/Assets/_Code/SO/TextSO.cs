using System;
using UnityEngine;

namespace _Code.SO
{
    public enum TextType
    {
        Clear,
        Double,
        Tripple,
        Quadra,
        PointX2
    }
    [CreateAssetMenu(fileName = "Event/Text", menuName = "Event/Text", order = 0)]
    public class TextSO : ScriptableObject
    {
        public TextType EventType;
        public string Text;
        public Color TxtColor;

        private void OnValidate()
        {
            if (TxtColor.a <= 0)
                TxtColor.a = 1;
        }
    }
}
