using System.Numerics;
using Aquarium;

namespace Aquarium.Sample;

/// <summary>
/// An actual aquarium: schools of fish, bubbles and swaying plants behind glass.
/// </summary>
internal sealed class AquariumDemo : IDemo
{
    private static readonly Vector3 Tank = new(16, 7, 8);
    private const float Sand = 0.6f;

    private readonly Random _random = new(3);
    private readonly InstanceBatch _bodies, _tails, _fins, _eyes, _bubbles, _plants;
    private readonly Fish[] _fish;
    private readonly Instance[] _bodyInstances, _tailInstances, _finInstances, _eyeInstances, _bubbleInstances, _plantInstances;
    private readonly (Vector3 Base, float Phase, int Segments)[] _stems;
    private float _time;

    private struct Fish
    {
        public Vector3 Position, Velocity, Target;
        public float Length, Speed, Phase;
        public Color4 Color;
    }

    public AquariumDemo(int scale)
    {
        Scene = new Scene { Background = new Color4(18, 32, 48) };

        // Sand, pebbles and a few rocks
        var ground = new List<Instance> { Instance.FromCorner(Vector3.Zero, new Vector3(Tank.X, Tank.Y, Sand), new Color4(222, 199, 150)) };
        var pebbles = new List<Instance>();
        for (int i = 0; i < 160; i++)
        {
            float s = 0.15f + (float)_random.NextDouble() * 0.25f;
            pebbles.Add(new Instance(new Vector3(Rand(0.3f, Tank.X - 0.3f), Rand(0.3f, Tank.Y - 0.3f), Sand), new Vector3(s, s * 0.9f, s * 0.5f),
                Color4.FromHsv(25 + _random.NextDouble() * 20, 0.15 + _random.NextDouble() * 0.2, 0.45 + _random.NextDouble() * 0.4)));
        }
        foreach (var (x, y, s) in new[] { (3.5f, 4.8f, 1.6f), (4.6f, 5.4f, 1.0f), (12.5f, 2.2f, 1.3f) })
            pebbles.Add(new Instance(new Vector3(x, y, Sand + s * 0.25f), new Vector3(s * 1.4f, s, s * 1.1f), new Color4(120, 115, 110)));
        Scene.Add(new InstanceBatch(Mesh.Box, "Sand") { Pickable = false, ShowEdges = false }).Set(ground.ToArray());
        Scene.Add(new InstanceBatch(Mesh.Sphere, "Pebbles") { ShowEdges = false }).Set(pebbles.ToArray());

        // Plants: stacks of short cylinders swaying in the current
        _stems = Enumerable.Range(0, 26).Select(_ =>
        {
            bool back = _random.NextDouble() < 0.7;
            var position = new Vector3(Rand(0.6f, Tank.X - 0.6f), back ? Rand(Tank.Y * 0.6f, Tank.Y - 0.5f) : Rand(0.5f, Tank.Y - 0.5f), Sand);
            return (position, Rand(0, MathF.Tau), 8 + _random.Next(10));
        }).ToArray();
        _plantInstances = new Instance[_stems.Sum(s => s.Segments)];
        _plants = Scene.Add(new InstanceBatch(Mesh.Cylinder, "Plants") { ShowEdges = false });

        // Fish: a few schools, each with its own color and size
        int schools = 5 * scale;
        var fish = new List<Fish>();
        for (int s = 0; s < schools; s++)
        {
            Color4 color = Color4.FromHsv(new[] { 15, 35, 50, 190, 280, 340 }[s % 6] + _random.NextDouble() * 10, 0.75, 0.98);
            float length = Rand(0.45f, 0.9f);
            Vector3 home = RandomPoint();
            for (int i = 0, n = 6 + _random.Next(10); i < n; i++)
                fish.Add(new Fish
                {
                    Position = home + new Vector3(Rand(-1, 1), Rand(-1, 1), Rand(-0.5f, 0.5f)),
                    Velocity = new Vector3(Rand(-1, 1), Rand(-1, 1), 0),
                    Target = home,
                    Length = length * Rand(0.85f, 1.15f),
                    Speed = Rand(0.8f, 1.4f) / MathF.Sqrt(length),
                    Phase = Rand(0, MathF.Tau),
                    Color = color,
                });
        }
        _fish = fish.ToArray();
        _bodyInstances = new Instance[_fish.Length];
        _tailInstances = new Instance[_fish.Length];
        _finInstances = new Instance[_fish.Length];
        _eyeInstances = new Instance[_fish.Length * 2];
        _bodies = Scene.Add(new InstanceBatch(Mesh.Sphere, "Fish") { ShowEdges = false });
        _tails = Scene.Add(new InstanceBatch(Mesh.Box, "Tails") { Pickable = false, ShowEdges = false });
        _fins = Scene.Add(new InstanceBatch(Mesh.Box, "Fins") { Pickable = false, ShowEdges = false });
        _eyes = Scene.Add(new InstanceBatch(Mesh.Sphere, "Eyes") { Pickable = false, ShowEdges = false });

        // Bubbles rising from two stones
        _bubbleInstances = new Instance[60];
        for (int i = 0; i < _bubbleInstances.Length; i++)
            _bubbleInstances[i] = NewBubble(Rand(Sand, Tank.Z));
        _bubbles = Scene.Add(new InstanceBatch(Mesh.Sphere, "Bubbles") { Pickable = false, ShowEdges = false, Transparent = true });

        // Water and glass
        Scene.Add(new InstanceBatch(Mesh.Box, "Water") { Pickable = false, ShowEdges = false, Transparent = true })
            .Set(new[] { Instance.FromCorner(new Vector3(0, 0, Sand), new Vector3(Tank.X, Tank.Y, Tank.Z * 0.92f - Sand), new Color4(90, 170, 220, 40)) });
        var glass = new List<Line>();
        LineBatch.AddBoxEdges(glass, new BoundingBox(Vector3.Zero, Tank), new Color4(200, 230, 245));
        LineBatch.AddBoxEdges(glass, new BoundingBox(new Vector3(-0.15f, -0.15f, -0.4f), new Vector3(Tank.X + 0.15f, Tank.Y + 0.15f, 0)), new Color4(60, 60, 60));
        Scene.Add(new LineBatch("Glass")).Set(glass.ToArray());

        Animate(0);
        Description = $"Aquarium: {_fish.Length} fish, {_stems.Length} plants, {_bubbleInstances.Length} bubbles";
    }

    public Scene Scene { get; }
    public string Description { get; }

    public void SetupCamera(Camera camera)
    {
        camera.Yaw = -MathF.PI / 2 - 0.35f;
        camera.Pitch = 0.3f;
    }

    private float Rand(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    private Vector3 RandomPoint() => new(Rand(1.5f, Tank.X - 1.5f), Rand(1.2f, Tank.Y - 1.2f), Rand(Sand + 1.2f, Tank.Z - 1.5f));

    private Instance NewBubble(float z)
    {
        float size = Rand(0.06f, 0.16f);
        var source = _random.NextDouble() < 0.5 ? new Vector2(3.5f, 4.8f) : new Vector2(12.5f, 2.2f);
        return new Instance(new Vector3(source.X + Rand(-0.2f, 0.2f), source.Y + Rand(-0.2f, 0.2f), z), new Vector3(size), new Color4(230, 245, 255, 120));
    }

    public void Animate(float seconds)
    {
        _time += seconds;
        AnimateFish(seconds);

        // Bubbles: rise with a little wobble, restart at the bottom
        for (int i = 0; i < _bubbleInstances.Length; i++)
        {
            ref Instance b = ref _bubbleInstances[i];
            b.Position += new Vector3(MathF.Sin(_time * 3 + i) * 0.3f * seconds, 0, (1.2f + b.Size.X * 4) * seconds);
            if (b.Position.Z > Tank.Z * 0.92f) b = NewBubble(Sand + 0.3f);
        }
        _bubbles.Set(_bubbleInstances);

        // Plants: each segment leans a bit more than the one below
        int k = 0;
        foreach (var (basePosition, phase, segments) in _stems)
        {
            Vector3 p = basePosition;
            for (int s = 0; s < segments; s++)
            {
                float t = s / (float)segments;
                float lean = 0.18f * t * MathF.Sin(_time * 0.9f + phase + s * 0.25f);
                Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, lean) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, lean * 0.6f);
                var up = Vector3.Transform(new Vector3(0, 0, 0.32f), rotation);
                float width = 0.2f * (1 - t * 0.6f);
                _plantInstances[k++] = new Instance(p + up / 2, new Vector3(width, width, 0.34f), rotation,
                    Color4.FromHsv(110 + phase * 4, 0.6, 0.45 + t * 0.35));
                p += up;
            }
        }
        _plants.Set(_plantInstances);
    }

    private void AnimateFish(float seconds)
    {
        var min = new Vector3(0.8f, 0.8f, Sand + 0.7f);
        var max = new Vector3(Tank.X - 0.8f, Tank.Y - 0.8f, Tank.Z * 0.92f - 0.6f);
        for (int i = 0; i < _fish.Length; i++)
        {
            ref Fish f = ref _fish[i];
            if (seconds > 0)
            {
                // Schools share a target, which moves on once the first fish arrives
                if (Vector3.Distance(f.Position, f.Target) < 1.2f)
                {
                    Vector3 target = RandomPoint();
                    for (int j = 0; j < _fish.Length; j++)
                        if (_fish[j].Color == f.Color) _fish[j].Target = target;
                }
                Vector3 desired = Vector3.Normalize(f.Target - f.Position + new Vector3(MathF.Sin(_time + f.Phase), MathF.Cos(_time * 0.7f + f.Phase), 0) * 0.8f) * f.Speed;
                desired.Z *= 0.4f; // Fish mostly swim horizontally
                f.Velocity = Vector3.Lerp(f.Velocity, desired, MathF.Min(1, seconds * 1.5f));
                f.Position = Vector3.Clamp(f.Position + f.Velocity * seconds, min, max);
            }

            // Orientation: nose along +x (local), turned by yaw and pitched by the vertical speed
            float yaw = MathF.Atan2(f.Velocity.Y, f.Velocity.X);
            float pitch = -MathF.Atan2(f.Velocity.Z, new Vector2(f.Velocity.X, f.Velocity.Y).Length());
            Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, pitch);
            Vector3 position = f.Position;
            float length = f.Length;
            Vector3 Local(float x, float y, float z) => position + Vector3.Transform(new Vector3(x, y, z) * length, rotation);

            float wiggle = MathF.Sin(_time * 9 * f.Speed + f.Phase) * 0.45f;
            Quaternion tail = rotation * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, wiggle) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 4);
            Color4 fin = new((byte)(f.Color.R * 0.8f), (byte)(f.Color.G * 0.8f), (byte)(f.Color.B * 0.8f));
            _bodyInstances[i] = new Instance(f.Position, new Vector3(1, 0.38f, 0.55f) * f.Length, rotation, f.Color);
            _tailInstances[i] = new Instance(Local(-0.6f, 0, 0) + Vector3.Transform(new Vector3(0, wiggle * 0.08f, 0), rotation), new Vector3(0.3f, 0.03f, 0.3f) * f.Length, tail, fin);
            _finInstances[i] = new Instance(Local(0, 0, 0.3f), new Vector3(0.35f, 0.03f, 0.18f) * f.Length, rotation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.35f), fin);
            _eyeInstances[2 * i] = new Instance(Local(0.32f, 0.14f, 0.07f), new Vector3(0.09f * f.Length), new Color4(20, 20, 30));
            _eyeInstances[2 * i + 1] = new Instance(Local(0.32f, -0.14f, 0.07f), new Vector3(0.09f * f.Length), new Color4(20, 20, 30));
        }
        _bodies.Set(_bodyInstances);
        _tails.Set(_tailInstances);
        _fins.Set(_finInstances);
        _eyes.Set(_eyeInstances);
    }

    public void Highlight(PickResult hit)
    {
        if (hit.Batch == _bodies)
        {
            Color4 school = _fish[hit.Index].Color, golden = new(255, 215, 0);
            for (int i = 0; i < _fish.Length; i++)
                if (_fish[i].Color == school) _fish[i].Color = golden;
        }
    }
}
