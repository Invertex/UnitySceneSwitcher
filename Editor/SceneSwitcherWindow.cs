using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Editor.SceneSwitcher
{
    public class SceneSwitcherWindow : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Window/SceneSwitcher")]
        private static void OpenWindow()
        {
            var window = GetWindow<SceneSwitcherWindow>(false, "Scenes", true);
            window.Show();
        }

        private Vector2 _scrollPosition;

        [System.Serializable]
        private class SceneSwitcherData
        {
            public List<SceneData> scenes = new List<SceneData>();
            public bool sortRecentToTop = true;
            public bool loadAdditive = true;
            public bool closeScenes = true;

            public void AddScene(string guid)
            {
                if (!scenes.Exists((scene) => scene.guid == guid))
                {
                    var sceneData = new SceneData() { guid = guid, color = Color.white };
                    
                    scenes.Add(sceneData);
                }
            }

            [System.Serializable]
            internal class SceneData
            {
                public string guid;
                public Color color;
            }
        }

        private SceneSwitcherData _sceneSwitcherData = new SceneSwitcherData();

        private const string PrefsKey = "EditorSceneSwitcher";

        private void OnEnable()
        {
            //Load State
            var jsonData = PlayerPrefs.GetString(PrefsKey, null);
            if (string.IsNullOrEmpty(jsonData))
            {
                return;
            }

            _sceneSwitcherData = JsonUtility.FromJson<SceneSwitcherData>(jsonData);
        }

        private void OnDisable()
        {
            //Save State
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_sceneSwitcherData));
        }

        private bool _editing;

        private void OnGUI()
        {
            if (Application.isPlaying)
            {
                var scene = SceneManager.GetActiveScene();
                GUILayout.Label("Scene Switching Disabled While Playing");
                GUILayout.Label($"Active Scene: {scene.name}");
                GUILayout.Label($"Total Loaded Scenes: {SceneManager.sceneCount}");
                return;
            }

            var evt = Event.current;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = IsValidSelection(DragAndDrop.objectReferences)
                        ? DragAndDropVisualMode.Generic
                        : DragAndDropVisualMode.Rejected;
                    break;
                case EventType.DragPerform:
                    AddObjects(DragAndDrop.objectReferences);
                    return;
                case EventType.DragExited:
                    break;
                default:
                    //Other events ignored
                    break;
            }

            if (_sceneSwitcherData.scenes.Count > 0)
            {
                SceneListGui();
            }
            else
            {
                BoxGui("Drop Scene Assets Here");
            }
        }

        private static void BoxGui(string text)
        {
            var rect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            const int padding = 10;
            rect.x += padding;
            rect.y += padding;
            rect.width -= padding * 2;
            rect.height -= padding * 2;

            var boxStyle = new GUIStyle("GroupBox");
            GUI.Box(rect, text, boxStyle);
        }

        private void SceneListGui()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            GUILayout.BeginVertical(new GUIStyle("GroupBox"));
            foreach (var sceneData in _sceneSwitcherData.scenes)
            {
                GUILayout.BeginHorizontal();

                if (_editing)
                {
                    if (GUILayout.Button("↑", GUILayout.MaxWidth(20)))
                    {
                        MoveUp(sceneData);
                        GUILayout.EndHorizontal();
                        break;
                    }
                    else if (GUILayout.Button("↓", GUILayout.MaxWidth(20)))
                    {
                        MoveDown(sceneData);
                        GUILayout.EndHorizontal();
                        break; 
                    }
                }

                var path = AssetDatabase.GUIDToAssetPath(sceneData.guid);
                var preColorBG = GUI.backgroundColor;
                GUI.backgroundColor = sceneData.color;
                
                if (GUILayout.Button(System.IO.Path.GetFileNameWithoutExtension(path)))
                {
                    GUI.backgroundColor = preColorBG;
                    // Give user option to save/cancel
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        GUILayout.EndHorizontal();
                        break;
                    }

                    SwitchToScene(path);
                    if (_sceneSwitcherData.sortRecentToTop)
                    {
                        MoveToTop(sceneData);
                    }

                    GUILayout.EndHorizontal();
                    break;
                }

                GUI.backgroundColor = preColorBG;

                if (_editing)
                {
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("X", GUILayout.MaxWidth(20)))
                    {
                        _sceneSwitcherData.scenes.Remove(sceneData);
                        GUILayout.EndHorizontal();
                        break;
                    }

                    sceneData.color = EditorGUILayout.ColorField(sceneData.color, GUILayout.Width(40f));

                    GUI.backgroundColor = Color.white;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            const int lineHeight = 18;
            if (_editing)
            {
                //Draw Toggle Buttons
                GUILayout.BeginHorizontal();
                _sceneSwitcherData.sortRecentToTop = GUILayout.Toggle(_sceneSwitcherData.sortRecentToTop,
                    new GUIContent("Auto Sort", "Will sort most recently used scenes to the top"),
                    GUILayout.Height(lineHeight));
                _sceneSwitcherData.loadAdditive = GUILayout.Toggle(_sceneSwitcherData.loadAdditive,
                    new GUIContent("Additive", "Loads scenes additively"), GUILayout.Height(lineHeight));
                _sceneSwitcherData.closeScenes = GUILayout.Toggle(_sceneSwitcherData.closeScenes,
                    new GUIContent("Close", "Will close/unload other scenes when additive loading is active"),
                    GUILayout.Height(lineHeight));
                GUILayout.EndHorizontal();
            
                //Draw Done Button
                GUILayout.BeginHorizontal();

                GUILayout.FlexibleSpace();
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("Done", GUILayout.MaxWidth(60)))
                {
                    _editing = !_editing;
                }
                GUI.backgroundColor = Color.white;

                GUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void MoveToTop(SceneSwitcherData.SceneData guid)
        {
            _sceneSwitcherData.scenes.Remove(guid);
            _sceneSwitcherData.scenes.Insert(0, guid);
        }

        private void MoveUp(SceneSwitcherData.SceneData guid)
        {
            var index = _sceneSwitcherData.scenes.IndexOf(guid);
            _sceneSwitcherData.scenes.RemoveAt(index);
            if (index > 0)
            {
                index--;
            }

            _sceneSwitcherData.scenes.Insert(index, guid);
        }

        private void MoveDown(SceneSwitcherData.SceneData guid)
        {
            var index = _sceneSwitcherData.scenes.IndexOf(guid);
            _sceneSwitcherData.scenes.RemoveAt(index);
            if (index < _sceneSwitcherData.scenes.Count)
            {
                index++;
            }

            _sceneSwitcherData.scenes.Insert(index, guid);
        }

        private void SwitchToScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path,
                _sceneSwitcherData.loadAdditive ? OpenSceneMode.Additive : OpenSceneMode.Single);

            if (!_sceneSwitcherData.closeScenes)
            {
                return;
            }

            //Close other scenes
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var otherScene = SceneManager.GetSceneAt(i);
                if (otherScene.isLoaded && otherScene != scene)
                {
                    EditorSceneManager.CloseScene(otherScene, false);
                }
            }
        }

        private void AddObjects(Object[] objects)
        {
            foreach (var obj in objects)
            {
                var sceneAsset = obj as SceneAsset;

                if (sceneAsset == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(sceneAsset);
                var guid = AssetDatabase.AssetPathToGUID(path);

                _sceneSwitcherData.AddScene(guid);
            }
        }

        private static bool IsValidSelection(IEnumerable<Object> objects)
        {
            return objects.Select(t => t as SceneAsset).All(sceneAsset => sceneAsset != null);
        }

        #region  IHasCustomMenu Implementation

        public void AddItemsToMenu(GenericMenu menu)
        {
            GUIContent content = new GUIContent("Edit Mode");
            menu.AddItem(content, _editing, ToggleEdit);
        }

        private void ToggleEdit()
        {
            _editing = !_editing;
        }

        #endregion
        
    }
}
