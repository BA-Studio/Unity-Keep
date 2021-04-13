using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Recents : EditorWindow, ISerializationCallbackReceiver
{
    static readonly int SIZE = 30;
    bool initialized;
    [SerializeField] Item[] cache;
    Queue<Item> recentObjects;
    Stack<Item> pool;

    [MenuItem("Window/BAStudio/Recents")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(Recents));
    }

    GUIStyle styleAvailable, styleUnavailable;
    GUIContent outOfScope = new GUIContent("Selected object\nis out of scope!");

    void OnEnable ()
    {
        initialized = false;
    }

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

        EditorGUIUtility.SetIconSize(new Vector2(24f, 24f));
        if (recentObjects.Count == 0) return;

        Rect rect;
        int size, index;
        size = recentObjects.Count;
        index = 0;

        using (var e = recentObjects.GetEnumerator())
        {
            while (e.MoveNext())
            {
                int y = (size - index - 1) * 33;
                if (y > position.height)
                {
                    index++;
                    continue;
                }

                bool available = true;
                if (e.Current.obj == null) available = false;
                if (e.Current.guiContent == null)
                    e.Current.guiContent = new GUIContent(EditorGUIUtility.ObjectContent(UnityEditor.Selection.activeObject, null));

                rect = new Rect(0, y, position.width, 32);
                if (GUI.Button(rect, e.Current.guiContent, available? styleAvailable : styleUnavailable))
                {
                    
                    if (available)
                    {
                        
                        if (Event.current.button == 1) // Right mouse button
                        {
                            EditorWindow.GetWindow<Laters>().AddItem(e.Current.obj);
                        }
                        else
                        {
                            Selection.SetActiveObjectWithContext(e.Current.obj, null);
                            selectingWithin = e.Current.obj;
                        }
                    }
                    else ShowNotification(outOfScope);
                }
                index++;
            }
        }
    }

    void Initialize()
    {
        if (recentObjects == null) recentObjects = new Queue<Item>();
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
        recentObjects.Enqueue(i);
        if (recentObjects.Count >= SIZE) pool.Push(recentObjects.Dequeue());
        Repaint();
    }
    public void OnBeforeSerialize()
    {
        if (cache == null) cache = new Item[SIZE];
        else
        {
            for (int i = 0; i < cache.Length; i++) cache[i] = null;
        }
        int index = 0;
        using (var e = recentObjects.GetEnumerator())
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
        if (recentObjects == null) recentObjects = new Queue<Item>(SIZE);
        else recentObjects.Clear();

        if (cache == null) return;

        for (int i = 0; i < cache.Length; i++)
        {
            if (cache[i] == null || cache[i].obj == null) continue;
            recentObjects.Enqueue(cache[i]);
        }
    }

    [Serializable]
    class Item
    {
        public UnityEngine.Object obj;
        public GUIContent guiContent;
    }
}
