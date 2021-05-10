using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BAStudio.Keep
{
    public class Laters : EditorWindow, ISerializationCallbackReceiver, IHasCustomMenu
    {
        static readonly int SIZE = 30;
        public static Laters Instance { get; private set; }
        bool initialized;
        [SerializeField] Item[] cache;
        Queue<Item> items;
        List<Item> itemsReversed;
        HashSet<UnityEngine.Object> unityObjects;
        Stack<Item> pool;

        [MenuItem("Window/BAStudio/Laters")]
        public static void ShowWindow()
        {
            if (Instance != null)
            {
                Instance.ShowNotification(new GUIContent("I'm here!"));
                return;
            }
            Instance = EditorWindow.GetWindow<Laters>("Keep.Laters");
        }

        void OnEnable ()
        {
            if (Instance == null) Instance = this;
            initialized = false;
        }

        [SerializeField] Vector2 scroll;
        
        int upperCulled, midDrawn, lowerCulled;

        void OnGUI ()
        {
            if (!initialized) Initialize();

            bool repaint = false;

            switch (Event.current.type)
            {
                case EventType.DragUpdated:
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                    break;
                }
                case EventType.DragPerform:
                {
                    for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                    {
                        if (!AddItem(DragAndDrop.objectReferences[i], true)) break;
                    }

                    DragAndDrop.AcceptDrag();
                    Event.current.Use();

                    repaint = true;
                    // Repaint();
                    return;
                }
                case EventType.Layout:
                {
                    break;
                }
            }
            

            EditorGUIUtility.SetIconSize(new Vector2(24f, 24f));
            

            if (items.Count * Keep.ITEM_PADDED < position.height/3)
            {
                GUI.Label(new Rect(0, 0, position.width, position.height), "Left click to select\nRight click to Forevers\nLatest on top", Keep.StyleHint);
            }
            if (items.Count == 0) return;

            int size, index;
            size = items.Count;
            index = -1;

            using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll, GUIStyle.none, GUI.skin.verticalScrollbar))
            {
                scroll = scrollView.scrollPosition;

                // Optimization: Draw only visible control and compress all invisible control to 2 rects (upper/lower)
                // Calculate upper/lower palceholder height
                
                int itemCount = items.Count;

                // int maxDrawable = (int) Mathf.Ceil(position.height / (float) ITEM_PADDED);
                if (Event.current.type == EventType.Layout)
                {
                    upperCulled = (int) Mathf.Floor(scroll.y / (float) Keep.ITEM_PADDED);
                    lowerCulled = (int) Mathf.Floor((items.Count * Keep.ITEM_PADDED - scroll.y - position.height) / (float) Keep.ITEM_PADDED);
                    midDrawn = items.Count - upperCulled - lowerCulled;
                }

                // int firstVisibleIndex = (int) Mathf.Floor((upperBound - ITEM_SIZE) / (float) ITEM_PADDED) + 1;
                // if (scroll.y < ITEM_SIZE) firstVisibleIndex = 0; // The above calculation does not guarantee first item. Override
                // int lastVisibleIndex = (int) Mathf.Ceil(lowerBound / (float) ITEM_PADDED);

                if (upperCulled > 0) GUILayoutUtility.GetRect(position.width, upperCulled * Keep.ITEM_PADDED);

                // if (itemsReversed.Count != items.Count)
                // {
                //     itemsReversed.Clear();
                //     itemsReversed.AddRange(items.Reverse());
                // }
                using (var e = itemsReversed.GetEnumerator())
                {
                    while (e.MoveNext())
                    {
                        index++;
                        // UnityEngine.Debug.LogFormat("Index: {0}, {1}~{2}, Scroll:{3} / {4}", index, firstVisibleIndex, lastVisibleIndex, scroll.y, position.height);
                        if (index < upperCulled || index >= itemCount - lowerCulled)
                        {
                            continue;
                        }

                        Rect r = GUILayoutUtility.GetRect(position.width, Keep.ITEM_SIZE);
                        GUILayout.Space(1);
                        if (DrawItem(e.Current, r)) repaint = true;
                    }
                }

                if (lowerCulled > 0) GUILayoutUtility.GetRect(position.width, lowerCulled * Keep.ITEM_PADDED);
            }

            if (repaint)
            {
                UpdateCount();
                Repaint();
            }
        }

        bool DrawItem (Item i, Rect r)
        {
            bool available = true, repaint = false;
            if (i.obj == null) available = false;
            if (i.guiContent == null)
                i.guiContent = Keep.NewGUIContentAnnotatePathIfFolder(i.obj);

            if (GUI.Button(r, i.guiContent, available? (selectingWithin == i.obj && Selection.activeObject == i.obj)? Keep.StyleItemSelected : Keep.StyleItem : Keep.StyleItemUnavailable))
            {
                if (Event.current.button == 1)
                {
                    if (available)
                    {
                        if (Forevers.Instance == null)
                            Forevers.ShowWindow();
                        Forevers.Instance.AddItem(i.obj);
                    }
                    else ShowNotification(Keep.outOfScope);
                }
                else
                {
                    if (available)
                    {
                        if (selectingWithin == i.obj && Selection.activeObject == i.obj && AssetDatabase.Contains(i.obj))
                        {
                            AssetDatabase.OpenAsset(i.obj);
                        }
                        else
                        {
                            Selection.SetActiveObjectWithContext(i.obj, null);
                            selectingWithin = i.obj;
                            repaint = true;
                        }
                    }
                    else ShowNotification(Keep.outOfScope);
                }
            }
            return repaint;
        }

        public bool AddItem (UnityEngine.Object obj, bool delayRepaint = false)
        {
            Initialize();
            if (!unityObjects.Add(obj))
            {
                return false;
            }
            Item item = GetItemFromPool();
            item.obj = obj;
            this.items.Enqueue(item);
            this.itemsReversed.Insert(0, item);
            Item i = GetItemFromPool();
            if (items.Count >= SIZE)
            {
                var d = items.Dequeue();
                unityObjects.Remove(d.obj);
                pool.Push(d);
                itemsReversed.Remove(d);
            }
            UpdateCount();
            if (!delayRepaint)
                Repaint();
            return true;
        }

        void UpdateCount ()
        {
            this.titleContent.text = string.Concat("Keep.Laters ", items.Count, "/", SIZE);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Clear"), false, Clear);
        }

        public void Clear ()
        {
            items.Clear();
            unityObjects.Clear();
            itemsReversed.Clear();
            UpdateCount();
        }

        void Initialize()
        {
            if (items == null) items = new Queue<Item>();
            if (itemsReversed == null) itemsReversed = new List<Item>();
            if (unityObjects == null) unityObjects = new HashSet<UnityEngine.Object>();
            if (pool == null) pool = new Stack<Item>(SIZE);
            initialized = true;
        }

        Item GetItemFromPool ()
        {
            Item i = (pool.Count > 0)? pool.Pop() : new Item();
            i.guiContent = null;
            return i;
        }

        UnityEngine.Object selectingWithin;

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
            Initialize();
            if (cache == null) return;
            itemsReversed.Clear();

            for (int i = 0; i < cache.Length; i++)
            {
                if (cache[i] == null || cache[i].obj == null) continue;
                
                if (!unityObjects.Add(cache[i].obj)) continue;
                items.Enqueue(cache[i]);
                itemsReversed.Insert(0, cache[i]);
            }
            UpdateCount();
        }

        [Serializable]
        class Item
        {
            public UnityEngine.Object obj;
            public GUIContent guiContent;
        }
    }
}