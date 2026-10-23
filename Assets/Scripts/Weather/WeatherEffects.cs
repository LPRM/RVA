using UnityEngine;

namespace RVA
{
    /// <summary>
    /// Shows the weather reported by WeatherService in the scene: sets the directional light (clear,
    /// rain or night), plays the rain particles while it rains, and lowers the ball's damping in the
    /// rain so it slides more. Applied again on every level, because each level builds a new ball.
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        [SerializeField] bool logEvents = true;

        [Header("References (weather and builder are found automatically when empty)")]
        [SerializeField] WeatherService weatherService;
        [SerializeField] MazeBuilder builder;
        [Tooltip("The scene's directional light.")]
        [SerializeField] Light sun;
        [Tooltip("Rain particle system (Play On Awake off). Played while it rains.")]
        [SerializeField] ParticleSystem rain;

        [Header("Clear (day)")]
        [SerializeField, Min(0f)] float clearIntensity = 1.2f;
        [SerializeField] Color clearColor = new Color(1f, 0.96f, 0.88f);

        [Header("Rain (day)")]
        [SerializeField, Min(0f)] float rainIntensity = 0.6f;
        [SerializeField] Color rainColor = new Color(0.7f, 0.74f, 0.8f);

        [Header("Night (wins over rain for the light)")]
        [SerializeField, Min(0f)] float nightIntensity = 0.25f;
        [SerializeField] Color nightColor = new Color(0.35f, 0.45f, 0.8f);

        [Header("Ball")]
        [Tooltip("The ball's damping is multiplied by this while it rains. Below 1 = less friction, so the ball slides more.")]
        [SerializeField, Range(0f, 1f)] float rainDampingMultiplier = 0.3f;

        float baseDamping = 0.3f; // replaced by the real ball's damping as soon as there is a ball

        void Awake()
        {
            if (weatherService == null)
            {
                weatherService = FindAnyObjectByType<WeatherService>();
            }
            if (builder == null)
            {
                builder = FindAnyObjectByType<MazeBuilder>();
            }
        }

        void OnBuilt(MazeBuilder maze)
        {
            // Each level builds a new ball with the default damping, so store it and apply the weather again.
            if (maze.Ball != null)
                baseDamping = maze.Ball.LinearDamping;

            Apply(weatherService != null ? weatherService.Current : new WeatherInfo());
        }

        void OnWeatherChange(WeatherInfo info)
        {
            Apply(info);
        }

        void OnEnable()
        {
            if (builder != null)
                builder.Built += OnBuilt;
            if (weatherService != null)
                weatherService.WeatherChanged += OnWeatherChange;
        }

        void OnDisable()
        {
            if (builder != null)
                builder.Built -= OnBuilt;
            if (weatherService != null)
                weatherService.WeatherChanged -= OnWeatherChange;
        }

        void Apply(WeatherInfo info)
        {
            bool raining = info.kind == WeatherKind.Rain;

            // Light: night wins, then rain, then clear.
            if (sun != null)
            {
                if (info.isNight)
                {
                    sun.intensity = nightIntensity;
                    sun.color = nightColor;
                }
                else if (raining)
                {
                    sun.intensity = rainIntensity;
                    sun.color = rainColor;
                }
                else
                {
                    sun.intensity = clearIntensity;
                    sun.color = clearColor;
                }
            }

            // Rain particles: only start or stop them when the state changes.
            if (rain != null)
            {
                if (raining && !rain.isPlaying)
                    rain.Play();
                else if (!raining && rain.isPlaying)
                    rain.Stop();
            }

            // Ball: less damping in the rain, always calculated from the base value.
            bool hasBall = builder != null && builder.Ball != null;
            if (hasBall)
                builder.Ball.LinearDamping = raining ? baseDamping * rainDampingMultiplier : baseDamping;

            if (logEvents)
                Debug.Log($"[WeatherEffects] Applied {info.kind}{(info.isNight ? " at night" : "")}: " +
                          $"light {(sun != null ? sun.intensity.ToString("0.00") : "no Sun set")}, " +
                          $"rain {(rain == null ? "no Rain set" : raining ? "on" : "off")}, " +
                          $"ball damping {(hasBall ? builder.Ball.LinearDamping.ToString("0.00") : "no ball")}", this);
        }
    }
        
}
