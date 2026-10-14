using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace SpaceSling.Presentation.UI.General
{
    // Add XSYS_SHOW_BUILD_TIME and/or XSYS_SHOW_FPS to the target's
    // Scripting Define Symbols when the corresponding diagnostics should be visible.
    public class BuildInfoDisplay : MonoBehaviour
    {
        [SerializeField]
        private TMPro.TMP_Text _text;

#if XSYS_SHOW_FPS
        private const float FpsSampleInterval = 1f;
        private float _fpsSampleElapsed;
        private int _fpsFrameCount;
#endif

        private string _buildInfo = "Build unknown · not built";

        private IEnumerator Start()
        {
            yield return LoadBuildInfoAsync();
#if !XSYS_SHOW_FPS
            if (_text != null)
            {
                _text.SetText(_buildInfo);
            }
#endif
        }
        
        [System.Serializable]
        private sealed class BuildInfoData
        {
            public string commitHash = string.Empty;
#if XSYS_SHOW_BUILD_TIME
            public string buildTime = string.Empty;
#endif
        }

        private IEnumerator LoadBuildInfoAsync()
        {
            string json = null;
    #if UNITY_WEBGL && !UNITY_EDITOR
            using (UnityWebRequest request = UnityWebRequest.Get($"{Application.streamingAssetsPath}/BuildInfo.json"))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    json = request.downloadHandler.text;
                }
            }
    #else
            string path = Path.Combine(Application.streamingAssetsPath, "BuildInfo.json");
            if (File.Exists(path))
            {
                json = File.ReadAllText(path);
            }

            yield return null;
    #endif

            if (string.IsNullOrWhiteSpace(json))
            {
                yield break;
            }

            BuildInfoData buildInfo = JsonUtility.FromJson<BuildInfoData>(json);

            if (buildInfo == null || string.IsNullOrWhiteSpace(buildInfo.commitHash))
            {
                yield break;
            }

#if XSYS_SHOW_BUILD_TIME
            if (string.IsNullOrWhiteSpace(buildInfo.buildTime))
            {
                yield break;
            }

            _buildInfo = $"{buildInfo.commitHash} · {buildInfo.buildTime}";
#else
            _buildInfo = buildInfo.commitHash;
#endif
        }

#if XSYS_SHOW_FPS
        private void Update()
        {
            _fpsSampleElapsed += Time.unscaledDeltaTime;
            _fpsFrameCount++;
            if (_fpsSampleElapsed < FpsSampleInterval)
            {
                return;
            }

            int framesPerSecond = Mathf.RoundToInt(_fpsFrameCount / _fpsSampleElapsed);
            if (_text != null)
            {
                _text.SetText($"{_buildInfo} · {framesPerSecond} FPS");
            }

            _fpsSampleElapsed = 0f;
            _fpsFrameCount = 0;
        }
#endif
    }
}
