using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BAStudio.Keep
{
    public class Recents : EditorWindow, ISerializationCallbackReceiver
    {
        public static Recents Instance { get; private set; }
        static readonly int SIZE = 30, ITEM_SIZE = 32, PADDING = 1, ITEM_PADDED = 33;
        bool initialized;
        [SerializeField] Item[] cache;
        Queue<Item> items;
        Stack<Item> pool;

        [MenuItem("Window/BAStudio/Recents")]
        public static void ShowWindow()
        {
            if (Instance != null)
            {
                Instance.ShowNotification(new GUIContent("I'm here!"));
                return;
            }
            Instance = EditorWindow.GetWindow<Recents>("Keep.Recents");
        }

        void OnEnable ()
        {
            if (Instance == null) Instance = this;
            initialized = false;
        }

        void OnGUI ()
        {
            if (!initialized) Initialize();

            EditorGUIUtility.SetIconSize(new Vector2(24f, 24f));

            if (items.Count * ITEM_PADDED < position.height/3)
            {
                if (Laters.Instance != null)
                    GUI.Label(new Rect(0, 0, position.width, position.height), "Left click to select\nRight click to Laters\nLatest on top", Keep.StyleHint);
                else if (Forevers.Instance != null)
                    GUI.Label(new Rect(0, 0, position.width, position.height), "Left click to select\nRight click to Forevers\nLatest on top", Keep.StyleHint);
                else GUI.Label(new Rect(0, 0, position.width, position.height), "Left click to select\nRight click to Laters\nLatest on top", Keep.StyleHint);
            }

            if (items.Count == 0) return;

            int size, index;
            size = items.Count;
            index = -1;

            using (var e = items.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    index++;
                    int y = (size - index - 1) * ITEM_PADDED;
                    if (y > position.height)
                    {
                        continue;
                    }

                    Rect r = new Rect(0, y, position.width, ITEM_SIZE);
                    DrawItem(e.Current, r);
                }
            }
        }

        void DrawItem (Item i, Rect r)
        {
            bool available = true;
            if (i.obj == null) available = false;
            if (i.guiContent == null)
                i.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(i.obj, null));

            if (GUI.Button(r, i.guiContent, available? (selectingWithin == i.obj && Selection.activeObject == i.obj)? Keep.StyleItemSelected : Keep.StyleItem : Keep.StyleItemUnavailable))
            {
                
                if (available)
                {
                    
                    if (Event.current.button == 1) // Right mouse button
                    {
                        if (Laters.Instance != null) Laters.Instance.AddItem(i.obj);
                        else if (Forevers.Instance != null) Forevers.Instance.AddItem(i.obj);
                        else
                        {
                            Laters.ShowWindow();
                            Laters.Instance.AddItem(i.obj);
                        }
                    }
                    else
                    {
                        if (selectingWithin == i.obj && Selection.activeObject == i.obj && AssetDatabase.Contains(i.obj))
                        {
                            AssetDatabase.OpenAsset(i.obj);
                        }
                        else
                        {
                            Selection.SetActiveObjectWithContext(i.obj, null);
                            selectingWithin = i.obj;
                            Repaint();
                        }
                    }
                }
                else ShowNotification(Keep.outOfScope);
            }
        }

        void Initialize()
        {
            if (items == null) items = new Queue<Item>();
            pool = new Stack<Item>(SIZE);
            initialized = true;
        }

        Item GetItemFromPool ()
        {
            if (pool.Count > 0) return pool.Pop();
            else return new Item();
        }

        UnityEngine.Object selectingWithin;

        void OnSelectionChange()
        {
            if (UnityEditor.Selection.activeObject == null) return;

            UnityEngine.Object last = UnityEditor.Selection.objects[Selection.objects.Length - 1];
            if (last == selectingWithin) return;

            Item i = GetItemFromPool();
            i.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(last, null));
            i.obj = last;
            items.Enqueue(i);
            if (items.Count >= SIZE) pool.Push(items.Dequeue());
            Repaint();
            selectingWithin = null;
        }
        public void OnBeforeSerialize()
        {
            if (cache == null) cache = new Item[SIZE];
            else
            {
                for (int i = 0; i < cache.Length; i++) cache[i] = null;
            }
            int index = 0;
            using (var e = items.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    cache[index] = e.Current;
                    index++;
                }
            }
        }

        public void OnAfterDeserialize()
        {
            if (items == null) items = new Queue<Item>(SIZE);
            else items.Clear();

            if (cache == null) return;

            for (int i = 0; i < cache.Length; i++)
            {
                if (cache[i] == null || cache[i].obj == null) continue;
                items.Enqueue(cache[i]);
            }
        }

        [Serializable]
        class Item
        {
            public UnityEngine.Object obj;
            public GUIContent guiContent;
        }
    }
}