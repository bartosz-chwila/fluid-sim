using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = System.Numerics.Vector2;
using Vector3 = UnityEngine.Vector3;


public class FluidSimulation : MonoBehaviour
{
    public class InteractionConfiguration
    {
        public float Radius;
        public float ForceCoefficient;
        public float MaxForce;
    }

    private class Particle
    {
        public GameObject ParticleGameObject;
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 Acceleration;

        private float SmoothingKernel(float radius, float distance, float maxForce)
        {
            var val = Math.Max(0, radius - distance);
            float normal = Mathf.InverseLerp(0, radius * radius * radius, val * val * val);
            return Mathf.Lerp(0, maxForce, normal);
        }

        public void DrawDebugLines()
        {
            var position = ParticleGameObject.transform.position;
            Debug.DrawLine(position,
                new Vector3(position.x + Velocity.X * 5,
                    position.y + Velocity.Y * 5, 0f));
        }

        public void InteractWithOtherParticle(Particle second, InteractionConfiguration config)
        {
            var distance = Vector2.Distance(Position, second.Position);
            if (Position.Equals(second.Position))
            {
                var particleInteractionForce = SmoothingKernel(config.Radius, distance, config.MaxForce) *
                                               config.ForceCoefficient *
                                               Vector2.Normalize(Position) * -1;
                Acceleration += particleInteractionForce;
            }
            else
            {
                var particleInteractionForce = SmoothingKernel(config.Radius, distance, config.MaxForce) *
                                               config.ForceCoefficient *
                                               Vector2.Normalize(Position - second.Position);
                Acceleration += particleInteractionForce;
            }
        }

        public void ApplyGlobalForces(IEnumerable<Vector2> forces)
        {
            foreach (var f in forces)
            {
                Acceleration += f;
            }
        }

        public void UpdateAcceleration()
        {
            Velocity += Acceleration;
        }
       
        public void UpdatePosition(float boundaryRetentionCoefficient)
        {
            Position += Velocity;
            ApplyBounds(boundaryRetentionCoefficient);
            ParticleGameObject.transform.position = new Vector3(Position.X, Position.Y, 0);
        }

        void ApplyBounds(float boundaryRetentionCoefficient)
        {
            if (Globals.MinY > Position.Y)
            {
                Position.Y = Globals.MinY;
                Velocity.Y *= -1 * boundaryRetentionCoefficient;
            }
            else if (Globals.MaxY < Position.Y)
            {
                Position.Y = Globals.MaxY;
                Velocity.Y *= -1 * boundaryRetentionCoefficient;
            }

            if (Globals.MinX > Position.X)
            {
                Position.X = Globals.MinX;
                Velocity.X *= -1 * boundaryRetentionCoefficient;
            }

            else if (Globals.MaxX < Position.X)
            {
                Position.X = Globals.MaxX;
                Velocity.X *= -1 * boundaryRetentionCoefficient;
            }
        }

        public void ApplyViscosity(float coefficient)
        {
            Acceleration -= Velocity * coefficient;
        }
    }

    public int sphereNumber;
    public int rowSize;
    public float spacing = 10;
    public float sphereScale = 0.1f;

    public GameObject spherePrefab;

    public float interactionRadius = 1f;
    public float interactionForceCoefficient = 0.1f;
    public float interactionMaxForce = 1f;

    public float boundaryRetentionCoefficient = 0.1f;
    public float viscosityCoefficient = 0.1f;

    private Vector3 _position;
    private Particle[] _particles;

    private readonly Vector2[] _globalForces =
    {
        new Vector2(0, -1) * Globals.GravityConstant
    };

    private void OnEditorChange()
    {
        _position = transform.position;

        if (_particles != null)
        {
            foreach (var particle in _particles)
            {
                Destroy(particle.ParticleGameObject);
            }
        }

        _particles = new Particle[sphereNumber];
        for (int i = 0; i < sphereNumber; i++)
        {
            var position = transform.position;
            var sphere = Instantiate(spherePrefab,
                new Vector3(position.x + (i % rowSize) * spacing,
                    position.y + i / rowSize * spacing),
                Quaternion.identity);
            sphere.transform.localScale *= sphereScale;
            _particles[i] = new Particle
            {
                Position = new Vector2(position.x + (i % rowSize) * spacing,
                    position.y + i / rowSize * spacing),
                ParticleGameObject = sphere,
                Velocity = new Vector2(0, 0),
                Acceleration = new Vector2(0, 0),
            };
        }
    }

    private void Awake()
    {
        OnEditorChange();
    }

    private void FixedUpdate()
    {
        if (_position != transform.position)
        {
            OnEditorChange();
        }

        PerformParticleUpdate();
    }

    void PerformParticleUpdate()
    {
        var config = new InteractionConfiguration
        {
            Radius = interactionRadius,
            ForceCoefficient = interactionForceCoefficient,
            MaxForce = interactionMaxForce
        };
        Parallel.For(0, _particles.Length, i =>
        {
            _particles[i].Acceleration = new Vector2();
            for (int j = 0; j < _particles.Length; j++)
            {
                if (i != j)
                {
                    _particles[i].InteractWithOtherParticle(_particles[j], config);
                }
            }

            _particles[i].ApplyGlobalForces(_globalForces);
            _particles[i].ApplyViscosity(viscosityCoefficient);
            _particles[i].UpdateAcceleration();
        });

        for (int i = 0; i < _particles.Length; i++)
        {
           _particles[i].UpdatePosition(boundaryRetentionCoefficient); 
        }
    }
}