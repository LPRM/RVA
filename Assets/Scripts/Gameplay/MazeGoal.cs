using UnityEngine;

namespace RVA
{
    /// <summary>
    /// Visual for an exit cell: a quiet idle glow and a burst when the player wins.
    /// Created by MazeBuilder; GameManager calls PlayWin.
    /// </summary>
    public class MazeGoal : MonoBehaviour
    {
        ParticleSystem burst;

        internal void Init(Color glow)
        {
            CreateIdle(glow);
            burst = CreateBurst(glow);
        }

        public void PlayWin()
        {
            if (burst != null)
                burst.Play();
        }

        public static void PlayWinOn(Transform root)
        {
            if (root == null)
                return;
            foreach (MazeGoal goal in root.GetComponentsInChildren<MazeGoal>(true))
                goal.PlayWin();
        }

        void CreateIdle(Color glow)
        {
            var go = new GameObject("Idle glow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.35f;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.15f;
            main.startSize = 0.12f;
            main.startColor = glow;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = -0.05f;

            var emission = ps.emission;
            emission.rateOverTime = 12f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;

            ApplyUnlit(ps, glow);
        }

        ParticleSystem CreateBurst(Color glow)
        {
            var go = new GameObject("Win burst");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.2f;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.4f;
            main.startSize = 0.18f;
            main.startColor = glow;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.15f;

            ApplyUnlit(ps, glow);
            return ps;
        }

        static void ApplyUnlit(ParticleSystem ps, Color glow)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", glow);
            else
                material.color = glow;
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
    }
}
