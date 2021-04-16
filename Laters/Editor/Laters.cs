using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

public class Laters : EditorWindow, ISerializationCallbackReceiver, IHasCustomMenu
{
    public static Laters Instance { get; private set; }
    static readonly int SIZE = 30, ITEM_SIZE = 32, PADDING = 1, ITEM_PADDED = 33;
    bool initialized;
    [SerializeField] Item[] cache;
    Queue<Item> items;
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
        Instance = EditorWindow.GetWindow<Laters>();
    }

    GUIStyle styleAvailable, styleUnavailable, styleHighlight;
    GUIContent outOfScope = new GUIContent("Selected object\nis out of scope!");
    GUIContent full = new GUIContent("Storage full!\nClear the list for\nbest productivity👊");

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

        if (styleAvailable == null)
        {
            styleAvailable = new GUIStyle(GUI.skin.button);
            styleAvailable.fontSize = 16;
            styleAvailable.alignment = TextAnchor.MiddleLeft;
        }
        if (styleUnavailable == null)
        {
            styleUnavailable = new GUIStyle(GUI.skin.button);
            styleUnavailable.normal.textColor = Color.grey;
            styleUnavailable.alignment = TextAnchor.MiddleLeft;
            styleUnavailable.fontSize = 12;
        }
        if (styleHighlight == null)
        {
            styleHighlight = new GUIStyle(GUI.skin.button);
            styleHighlight.normal.textColor = Color.yellow;
            styleHighlight.alignment = TextAnchor.MiddleLeft;
            styleHighlight.fontSize = 16;
            styleHighlight.fontStyle = FontStyle.Bold;
        }

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
                inPanelPing = null;
                break;
            }
        }
        

        EditorGUIUtility.SetIconSize(new Vector2(24f, 24f));
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
                upperCulled = (int) Mathf.Floor(scroll.y / (float) ITEM_PADDED);
                lowerCulled = (int) Mathf.Floor((items.Count * ITEM_PADDED - scroll.y - position.height) / (float) ITEM_PADDED);
                midDrawn = items.Count - upperCulled - lowerCulled;
            }

            // int firstVisibleIndex = (int) Mathf.Floor((upperBound - ITEM_SIZE) / (float) ITEM_PADDED) + 1;
            // if (scroll.y < ITEM_SIZE) firstVisibleIndex = 0; // The above calculation does not guarantee first item. Override
            // int lastVisibleIndex = (int) Mathf.Ceil(lowerBound / (float) ITEM_PADDED);

            if (upperCulled > 0) GUILayoutUtility.GetRect(position.width, upperCulled * ITEM_PADDED);

            using (var e = items.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    index++;
                    // UnityEngine.Debug.LogFormat("Index: {0}, {1}~{2}, Scroll:{3} / {4}", index, firstVisibleIndex, lastVisibleIndex, scroll.y, position.height);
                    if (index < upperCulled || index >= itemCount - lowerCulled)
                    {
                        continue;
                    }

                    Rect r = GUILayoutUtility.GetRect(position.width, ITEM_SIZE);
                    GUILayout.Space(1);
                    DrawItem(e.Current, r);
                }
            }

            if (lowerCulled > 0) GUILayoutUtility.GetRect(position.width, lowerCulled * ITEM_PADDED);
        }

        if (repaint)
        {
            UpdateCount();
            Repaint();
        }
    }

    void DrawItem (Item i, Rect r)
    {
        bool available = true;
        if (i.obj == null) available = false;
        if (i.guiContent == null)
            i.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(i.obj, null));

        if (GUI.Button(r, i.guiContent, available? inPanelPing == i.obj? styleHighlight : styleAvailable : styleUnavailable))
        {
            if (Event.current.button == (int) MouseButton.RightMouse)
            {
                if (available)
                {
                    if (Forevers.Instance == null)
                        Forevers.ShowWindow();
                    Forevers.Instance.AddItem(i.obj);
                }
                else ShowNotification(outOfScope);
            }
            else
            {
                if (available)
                {
                    Selection.SetActiveObjectWithContext(i.obj, null);
                    selectingWithin = i.obj;
                }
                else ShowNotification(outOfScope);
            }
        }
    }

    UnityEngine.Object inPanelPing;

    public bool AddItem (UnityEngine.Object obj, bool delayRepaint = false)
    {
        Initialize();
        if (!unityObjects.Add(obj))
        {
            inPanelPing = obj;
            Repaint();
            return false;
        }
        Item item = GetItemFromPool();
        item.obj = obj;
        item.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(obj, null));
        this.items.Enqueue(item);
        Item i = GetItemFromPool();
        if (items.Count >= SIZE)
        {
            var d = items.Dequeue();
            unityObjects.Remove(d.obj);
            pool.Push(d);
        }
        UpdateCount();
        if (!delayRepaint)
            Repaint();
        return true;
    }

    void UpdateCount ()
    {
        this.titleContent.text = string.Concat("Laters ", items.Count, "/", SIZE);
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
        if (items == null) items = new Queue<Item>();
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
        if (items == null) items = new Queue<Item>();
        else items.Clear();
        if (unityObjects == null) unityObjects = new HashSet<UnityEngine.Object>();
        else unityObjects.Clear();

        if (cache == null) return;

        for (int i = 0; i < cache.Length; i++)
        {
            if (cache[i] == null || cache[i].obj == null) continue;
            items.Enqueue(cache[i]);
            unityObjects.Add(cache[i].obj);
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
