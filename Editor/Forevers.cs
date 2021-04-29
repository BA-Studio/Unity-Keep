using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.UIElements;
using System.Text;

namespace BAStudio.Keep
{
    public class Forevers : EditorWindow, ISerializationCallbackReceiver, IHasCustomMenu
    {
        public static Forevers Instance { get; private set; }
        static readonly int SIZE = 30, ITEM_SIZE = 32, PADDING = 1, ITEM_PADDED = 33;
        bool initialized;
        [SerializeField] Item[] cache;
        HashSet<Item> items;
        HashSet<UnityEngine.Object> unityObjects;
        HashSet<Item> deferredRemoving;
        Stack<Item> pool;

        [MenuItem("Window/BAStudio/Forevers")]
        public static void ShowWindow()
        {
            if (Instance != null)
            {
                Instance.ShowNotification(new GUIContent("I'm here!"));
                return;
            }
            Instance = EditorWindow.GetWindow<Forevers>();
        }
        bool latersEnabled;

        void OnEnable ()
        {
            if (Instance == null) Instance = this;
            initialized = false;
        }

        void Awake ()
        {
            string[] stored = PlayerPrefs.GetString("Editor.BAStudio.Forevers").Split(';');
            foreach (string s in stored)
            {
                if (string.IsNullOrEmpty(s)) continue;
                UnityEngine.Object o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(s);
                AddItem(o, true);
            }
            Repaint();
        }

        void OnDestroy ()
        {
            OnBeforeSerialize();
            Save();
        }

        void Save ()
        {
            if (cache == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < cache.Length; i++)
            {
                if (cache[i] == null) continue;
                if (AssetDatabase.Contains(cache[i].obj))
                {
                    sb.Append(AssetDatabase.GetAssetPath(cache[i].obj));
                    sb.Append(";");
                }
            }
            // UnityEngine.Debug.Log(sb.ToString());
            PlayerPrefs.SetString("Editor.BAStudio.Forevers", sb.ToString());
        }

        [SerializeField] Vector2 scroll;
        int firstVisibleIndex, lastVisibleIndex;

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
                    if (deferredRemoving.Count > 0)
                    {
                        using (var e = deferredRemoving.GetEnumerator())
                        while (e.MoveNext())
                            RemoveItem(e.Current);
                        
                        repaint = true;
                        deferredRemoving.Clear();
                        UpdateCount();
                    }
                    break;
                }
            }
            
            EditorGUIUtility.SetIconSize(new Vector2(24f, 24f));
            
            if (items.Count * ITEM_PADDED < position.height/3)
            {
                GUI.Label(new Rect(0, 0, position.width, position.height), "Left click to select\nRight click to remove", Keep.StyleHint);
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
                if (Event.current.type == EventType.Layout)
                {
                float upperBound = scroll.y;
                float lowerBound = scroll.y + position.height;
                firstVisibleIndex = (int) Mathf.Floor((upperBound - ITEM_SIZE) / (float) ITEM_PADDED) + 1;
                if (upperBound < ITEM_SIZE) firstVisibleIndex = 0; // The above calculation does not guarantee first item. Override
                lastVisibleIndex = (int) Mathf.Ceil(lowerBound / (float) ITEM_PADDED);
                }

                if (firstVisibleIndex > 0) GUILayoutUtility.GetRect(position.width, firstVisibleIndex * ITEM_PADDED);

                using (var e = items.GetEnumerator())
                {
                    while (e.MoveNext())
                    {
                        index++;
                        // UnityEngine.Debug.LogFormat("Index: {0}, {1}~{2}, Scroll:{3} / {4} -> {5}", index, firstVisibleIndex, lastVisibleIndex, scroll.y, position.height, e.Current.obj.name);
                        if (index < firstVisibleIndex || index > lastVisibleIndex)
                        {
                            continue;
                        }

                        Rect r = GUILayoutUtility.GetRect(position.width, ITEM_SIZE);
                        GUILayout.Space(1);

                        bool available = true;
                        if (e.Current.obj == null) available = false;
                        if (e.Current.guiContent == null)
                            e.Current.guiContent = Keep.NewGUIContentAnnotatePathIfFolder(e.Current.obj);

                        if (GUI.Button(r, e.Current.guiContent, available? (selectingWithin == e.Current.obj && Selection.activeObject == e.Current.obj)? Keep.StyleItemSelected : Keep.StyleItem : Keep.StyleItemUnavailable))
                        {
                            if (Event.current.button == (int) MouseButton.RightMouse)
                            {
                                deferredRemoving.Add(e.Current);
                                repaint = true;
                                break;
                            }
                            else if (available)
                            {
                                if (selectingWithin == e.Current.obj && Selection.activeObject == e.Current.obj && AssetDatabase.Contains(e.Current.obj))
                                {
                                    AssetDatabase.OpenAsset(e.Current.obj);
                                }
                                else
                                {
                                    Selection.SetActiveObjectWithContext(e.Current.obj, null);
                                    selectingWithin = e.Current.obj;
                                    repaint = true;
                                }

                            }
                            else ShowNotification(Keep.outOfScope);
                        }
                    }
                }
                
                if (lastVisibleIndex < items.Count - 1) GUILayoutUtility.GetRect(position.width, (items.Count - 1 - lastVisibleIndex) * ITEM_PADDED);
            }


            if (repaint)
            {
                Repaint();
            }
        }

        public bool AddItem (UnityEngine.Object obj, bool delayRepaint = false)
        {
            Initialize();
            if (items.Count >= SIZE)
            {
                ShowNotification(Keep.full);
                return false;
            }
            if (!unityObjects.Add(obj)) return false;
            Item item = GetItemFromPool();
            item.obj = obj;
            this.items.Add(item);
            UpdateCount();
            if (!delayRepaint)
                Repaint();
            return true;
        }

        void RemoveItem (Item i)
        {
            unityObjects.Remove(i.obj);
            items.Remove(i);
        }

        void UpdateCount ()
        {
            this.titleContent.text = string.Concat("Forevers ", items.Count, "/", SIZE);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Clear"), false, Clear);
        }

        public void Clear ()
        {
            items.Clear();
            unityObjects.Clear();
            UpdateCount();
        }

        void Initialize()
        {
            if (items == null) items = new HashSet<Item>();
            if (unityObjects == null) unityObjects = new HashSet<UnityEngine.Object>();
            if (deferredRemoving == null) deferredRemoving = new HashSet<Item>();
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
            if (items == null) items = new HashSet<Item>();
            if (unityObjects == null) unityObjects = new HashSet<UnityEngine.Object>();
            else items.Clear();

            if (cache == null) return;

            for (int i = 0; i < cache.Length; i++)
            {
                if (cache[i] == null || cache[i].obj == null) continue;
                AddItem(cache[i].obj);
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