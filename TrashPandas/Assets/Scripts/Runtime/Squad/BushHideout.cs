using System.Collections;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>A bush: you dive in with a burst of leaves; it shivers now and then while you look around.</summary>
    public sealed class BushHideout : Hideout
    {
        public float Radius = 0.9f;
        Vector3 _rest;
        float _shakeT = -1f;
        ParticleSystem _leaves;
        public override string Prompt => "E: dive into the bush";
        protected override float ExtraReach => Radius * 0.5f;
        protected override Vector3 InsidePoint => transform.position + Vector3.up * 0.1f;
        protected override Vector3 ExitPoint(bool kicked) => transform.position + transform.forward * (Radius + (kicked ? 1.2f : 0.6f)) + Vector3.up * 0.05f;

        void Awake()
        {
            _rest = transform.localScale;
            _leaves = MakeLeaves();
        }

        ParticleSystem MakeLeaves()
        {
            var go = new GameObject("Leaves");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * Radius * 0.6f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 0.9f;
            main.startSpeed = 2.4f;
            main.startSize = 0.11f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.6f, 0.25f), new Color(0.45f, 0.75f, 0.3f));
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Radius * 0.6f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = LeafMaterial();
            return ps;
        }

        static Material s_leaf;
        static Material LeafMaterial()
        {
            if (s_leaf) return s_leaf;
            s_leaf = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            return s_leaf;
        }

        protected override IEnumerator OpenFx(bool fast) { Shake(); yield break; }
        protected override void OnEnteredFx() { Shake(); if (_leaves) _leaves.Emit(28); }

        public override void Wiggle() { if (_shakeT < 0f || _shakeT > 0.4f) Shake(0.5f); }

        void Shake(float strength = 1f) { _shakeT = 0f; _shakeStrength = strength; }
        float _shakeStrength = 1f;

        void Update()
        {
            if (_shakeT < 0f) return;
            _shakeT += Time.deltaTime;
            float k = Mathf.Exp(-_shakeT * 6f) * Mathf.Sin(_shakeT * 38f) * 0.1f * _shakeStrength;
            transform.localScale = _rest + new Vector3(k, -k * 0.6f, k);
            if (_shakeT > 0.7f) { transform.localScale = _rest; _shakeT = -1f; }
        }
    }
}
