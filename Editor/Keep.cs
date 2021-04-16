using UnityEngine;

namespace BAStudio.Keep
{
    public static class Keep
    {
        internal static readonly int ITEM_SIZE = 32, PADDING = 1, ITEM_PADDED = 33;
        static GUIStyle styleItem, styleItemUnavailable, styleItemSelected, styleHint;
        public static GUIStyle StyleItemUnavailable
        {
            get
            {
                if (styleItemUnavailable == null)
                {
                    styleItemUnavailable = new GUIStyle(GUI.skin.button);
                    styleItemUnavailable.normal.textColor = Color.grey;
                    styleItemUnavailable.alignment = TextAnchor.MiddleLeft;
                    styleItemUnavailable.fontStyle = FontStyle.Italic;
                    styleItemUnavailable.fontSize = 14;

                }
                return styleItemUnavailable;
            }
        }
        public static GUIStyle StyleItemSelected
        {
            get
            {
                if (styleItemSelected == null)
                {
                    styleItemSelected = new GUIStyle(GUI.skin.button);
                    styleItemSelected.normal = styleItemSelected.active;
                    styleItemSelected.alignment = TextAnchor.MiddleLeft;
                    styleItemSelected.fontStyle = FontStyle.Bold;
                    styleItemSelected.fontSize = 16;

                }
                return styleItemSelected;
            }
        }
        public static GUIStyle StyleHint
        {
            get
            {
                if (styleHint == null)
                {
                    styleHint = new GUIStyle(GUI.skin.label);
                    styleHint.normal.textColor = Color.grey;
                    styleHint.alignment = TextAnchor.MiddleCenter;
                    styleHint.fontSize = 12;
                }
                return styleHint;
            }
        }

        public static GUIStyle StyleItem
        {
            get
            {
                if (styleItem == null)
                {
                    styleItem = new GUIStyle(GUI.skin.button);
                    styleItem.alignment = TextAnchor.MiddleLeft;
                    styleItem.fontSize = 16;

                }
                return styleItem;
            }
        }

        internal static GUIContent outOfScope = new GUIContent("Selected object\nis out of scope!");
        internal static GUIContent full = new GUIContent("Storage full!\nClear the list for\nbest productivity👊");

    }
}