using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;

namespace BAStudio.Keep
{
    public class InMind : EditorWindow, ISerializationCallbackReceiver, IHasCustomMenu
    {
        public static int uid;
        public static InMind Instance { get; private set; }
        static readonly int SIZE = 30, ITEM_SIZE = 32, PADDING = 1, ITEM_PADDED = 33;
        bool initialized;
        [SerializeField] Item[] cache;
        HashSet<Item> items;
        HashSet<UnityEngine.Object> unityObjects;
        HashSet<Item> deferredRemoving;
        Stack<Item> pool;

        GUIStyle styleItemUID;

        [MenuItem("Window/BAStudio/InMind")]
        public static void ShowWindow()
        {
            if (Instance != null)
            {
                Instance.ShowNotification(new GUIContent("I'm here!"));
                return;
            }
            Instance = EditorWindow.GetWindow<InMind>("Keep.InMind");
            Instance.minSize = new Vector2(32), 32);
            Instance.wantsMouseMove = true;
        }
        bool latersEnabled;

        void OnEnable ()
        {
            if (Instance == null) Instance = this;

            initialized = false;

            uid = PlayerPrefs.GetInt("Editor.BAStudio.InMind:UID");
        }

        void OnDisable ()
        {
            PlayerPrefs.SetInt("Editor.BAStudio.InMind:UID", uid);
        }

        void Awake ()
        {
            string[] stored = PlayerPrefs.GetString("Editor.BAStudio.InMind").Split(';');
            for (int i = 0; i < stored.Length; i+=2)
            {
                if (string.IsNullOrEmpty(stored[i])) continue;
                UnityEngine.Object o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(stored[i]);
                AddItem(o, true, stored[i+1]);
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
                    sb.Append(cache[i].uid);
                    sb.Append(";");
                }
            }
            // UnityEngine.Debug.Log(sb.ToString());
            PlayerPrefs.SetString("Editor.BAStudio.InMind", sb.ToString());
        }

        [SerializeField] Vector2 scroll;
        int firstVisibleIndex, lastVisibleIndex;

        void OnGUI ()
        {

            if (styleItemUID == null)
            {
                styleItemUID = new GUIStyle(GUI.skin.label);
                styleItemUID.fontSize = 8;
                styleItemUID.alignment = TextAnchor.LowerCenter;
                styleItemUID.normal.textColor = new Color(214f/255f, 150f/255f, 0);
            }

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
                    }
                    break;
                }
            }
            
            EditorGUIUtility.SetIconSize(new Vector2(24f, 24f));

            if (items.Count == 0) return;

            int index = -1;

            int col = (int) Math.Floor(position.width / ITEM_SIZE);
            int row = (int) Math.Floor(position.height / ITEM_SIZE);
            (int c, int r) pos = (0, 0);
            string info = string.Empty;

            using (var e = items.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    index++;
                    if (pos.c >= col)
                    {
                        pos.c = 0;
                        pos.r++;
                    }
                    // UnityEngine.Debug.LogFormat("Index: {0}, {1}~{2}, Scroll:{3} / {4} -> {5}", index, firstVisibleIndex, lastVisibleIndex, scroll.y, position.height, e.Current.obj.name);
                    
                    

                    Rect r = new Rect(pos.c * ITEM_SIZE, pos.r * ITEM_SIZE, ITEM_SIZE, ITEM_SIZE);

                    bool available = true;
                    if (e.Current.obj == null) available = false;
                    if (e.Current.guiContent == null)
                    {
                        e.Current.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(e.Current.obj, null));
                        e.Current.guiContent.text = null;
                    }

                    if (GUI.Button(r, e.Current.guiContent, available? (selectingWithin == e.Current.obj && Selection.activeObject == e.Current.obj)? Keep.StyleItemSelected : Keep.StyleItem : Keep.StyleItemUnavailable))
                    {
                        if (Event.current.button == 1)
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

                    GUI.Label(new Rect(r.x, r.y + 4, r.width, r.height), e.Current.uid, styleItemUID);

                    if (r.Contains(Event.current.mousePosition))
                    {
                        if (e.Current.obj != null) info = e.Current.obj.name;
                        else info = "Warning: object inaccessible";
                    }

                    pos.c++;
                }
            }
            if (!string.IsNullOrEmpty(info))
            {
                var h = Mathf.Max(18, position.height * 0.33f);
                Rect rect = new Rect(0, position.height - h, position.width, h);
                EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.5f));
                GUI.Label(rect, info);
                repaint = true;
            }

            if (repaint)
            {
                Repaint();
            }
        }


        public bool AddItem (UnityEngine.Object obj, bool delayRepaint = false, string uid = null)
        {
            if (!initialized) Initialize();
            if (items.Count >= SIZE)
            {
                ShowNotification(Keep.full);
                return false;
            }
            if (!unityObjects.Add(obj)) return false;
            Item item = GetItemFromPool();
            item.obj = obj;
            if (uid == null)
            {
                item.uid = "#"+InMind.uid.ToString();
                InMind.uid++;
            }
            else item.uid = uid;
            this.items.Add(item);
            if (!delayRepaint)
                Repaint();
            return true;
        }

        void RemoveItem (Item i)
        {
            unityObjects.Remove(i.obj);
            items.Remove(i);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Clear"), false, Clear);
        }

        public void Clear ()
        {
            items.Clear();
            unityObjects.Clear();
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

            initialized = false;
            if (items == null) items = new HashSet<Item>();
            if (unityObjects == null) unityObjects = new HashSet<UnityEngine.Object>();
            else items.Clear();

            if (cache == null) return;

            for (int i = 0; i < cache.Length; i++)
            {
                if (cache[i] == null || cache[i].obj == null) continue;
                AddItem(cache[i].obj, true, cache[i].uid);
            }
        }

        [Serializable]
        class Item
        {
            public string uid;
            public UnityEngine.Object obj;
            public GUIContent guiContent;
        }
    }
}