using FezEditor.Actors;

namespace FezEditor.Components.Eddy;

public sealed class InstanceActorRegistry
{
    public IReadOnlyCollection<InstanceId> Instances => _actors.Keys;

    public IReadOnlyCollection<Actor> Actors => _actors.Values;

    private readonly Scene _scene;

    private readonly Dictionary<InstanceId, Actor> _actors = new();

    private readonly Dictionary<Actor, InstanceId> _instances = new();

    public InstanceActorRegistry(Scene scene)
    {
        _scene = scene;
    }

    public Actor GetOrCreateActor(InstanceId id)
    {
        if (_actors.TryGetValue(id, out var actor))
        {
            return actor;
        }

        actor = _scene.CreateActor();
        _actors[id] = actor;
        _instances[actor] = id;
        return actor;
    }

    public bool TryGetActor(InstanceId id, out Actor actor)
    {
        return _actors.TryGetValue(id, out actor!);
    }

    public Actor GetActor(InstanceId id)
    {
        return _actors[id];
    }

    public IEnumerable<Actor> GetActors<T>() where T : InstanceId
    {
        foreach (var (instance, actor) in _actors)
        {
            if (typeof(T) == instance.GetType())
            {
                yield return actor;
            }
        }
    }

    public bool TryGetInstance(Actor actor, out InstanceId id)
    {
        return _instances.TryGetValue(actor, out id!);
    }

    public void Destroy(InstanceId id)
    {
        if (!_actors.Remove(id, out var actor))
        {
            return;
        }

        _instances.Remove(actor);
        _scene.DestroyActor(actor);
    }
}