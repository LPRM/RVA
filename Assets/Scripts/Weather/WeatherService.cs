using System;
using UnityEngine.Networking;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RVA
{
    public enum WeatherKind { Clear, Rain }

    public struct WeatherInfo
    {
        public WeatherKind kind;
        public bool isNight;
        public bool fromApi;
    }

    [Serializable]
    class OpenMeteoResponse
    {
        public OpenMeteoCurrent current;  
    }

    [Serializable]
    class OpenMeteoCurrent
    {
        public int weather_code;
        public int is_day; //0 or 1
    }

    /// <summary>
    /// Fetches the current weather (Porto by default) from Open-Meteo each time a level starts and
    /// publishes it as a WeatherInfo: Clear or Rain, plus whether it is night. Other scripts read
    /// Current or subscribe to WeatherChanged; this script only reports the weather and does not change
    /// the scene. If the request fails, times out or the reply cannot be read, it falls back to clear
    /// daytime weather. Set Mode to a Force option to show a given weather without the API (e.g. for the
    /// video); W fetches again.
    /// </summary>
    public class WeatherService : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [Tooltip("Latitude.")]
        [SerializeField] float latitude = 41.15f;

        [Tooltip("Longitude.")]
        [SerializeField] float longitude = -8.61f;

        [Tooltip("API Timeout.")]
        [SerializeField] int timeout = 5;

        [SerializeField] bool logEvents = true;

        enum Mode { Live, ForceClear, ForceRain, ForceNight }
        [Tooltip("Change weather settings.")]
        [SerializeField] Mode mode;

        bool fetching = false;

        public WeatherInfo Current {get; private set; }
        public event Action<WeatherInfo> WeatherChanged;

        void OnLevelStarted(int level) { Refresh(); }

        void Awake()
        {
            if (game == null)
            {
                game = FindAnyObjectByType<GameManager>();
            }
        }

        void Update()
        {   
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.wasPressedThisFrame)
                {
                    Refresh();
                }   
            }
        }

        public void Publish(WeatherInfo info)
        {
            Current = info;

            if (logEvents)
            {
                Debug.Log($"[WeatherService] {Current.kind}, API={Current.fromApi}, DayState={(Current.isNight ? "night" : "day")}");
            }

            WeatherChanged?.Invoke(info);
        }

        WeatherKind KindFromCode(int code)
        {   
            //It returns Rain for 51–67, 71–77, 80–82, 85–86 and 95–99, and Clear for everything else
            if ((code >= 51 && code <= 67) || (code >= 71 && code <= 77) 
            || (code >= 80 && code <= 82) || (code >= 85 && code <= 86) || (code >= 95 && code <= 99))
            {
                return WeatherKind.Rain;
            }
            else
            {
                return WeatherKind.Clear;
            }
        }

        void PublishFallback()
        {
            WeatherInfo fallback = new()
            {
                fromApi = false,
                isNight = false,
                kind = WeatherKind.Clear
            };
            Publish(fallback);
        }

        void Refresh()
        {
            if (fetching)
            {
                return;
            }
            StartCoroutine(FetchRoutine());
        }

        void OnEnable()
        {
            if (game != null)
            {
                game.LevelStarted += OnLevelStarted;
            }
        }

        void OnDisable()
        {
            if (game != null)
            {
                game.LevelStarted -= OnLevelStarted;
            }
            fetching = false;
        }

        IEnumerator FetchRoutine()
        {   
            fetching = true;
            if (mode != Mode.Live)
            {
                if (mode == Mode.ForceClear)
                {
                    PublishFallback();
                }else if(mode == Mode.ForceNight)
                {
                    WeatherInfo fallback = new()
                    {
                        fromApi = false,
                        isNight = true,
                        kind = WeatherKind.Clear
                    };
                    Publish(fallback);
                }
                else
                {
                    WeatherInfo fallback = new()
                    {
                        fromApi = false,
                        isNight = false,
                        kind = WeatherKind.Rain
                    };
                    Publish(fallback);
                }

                fetching = false;
                yield break;
            }

            string url = string.Format(CultureInfo.InvariantCulture, "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=weather_code,is_day", latitude, longitude);
            using UnityWebRequest req = UnityWebRequest.Get(url);
            req.timeout = timeout;
            
            yield return req.SendWebRequest(); // waits here; the game keeps running
            if (req.result != UnityWebRequest.Result.Success)
            {
                PublishFallback();
            }
            else
            {
                OpenMeteoResponse response;
                try
                {
                    response = JsonUtility.FromJson<OpenMeteoResponse>(req.downloadHandler.text);
                    
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[WeatherService] Could not read the reply: {e.Message}");
                    response = null;
                }

                if (response == null || response.current == null)
                {
                    Debug.LogWarning($"[WeatherService] Request failed: {req.error}");
                    PublishFallback();
                }
                else
                {
                    WeatherInfo info = new()
                    {
                        fromApi = true,
                        isNight = response.current.is_day == 0 ? true : false,
                        kind = KindFromCode(response.current.weather_code)
                    };
                    Publish(info);
                }
            }
            fetching = false;
        }


    }
        
}
