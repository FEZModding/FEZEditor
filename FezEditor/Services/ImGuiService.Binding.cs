using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Services;

public partial class ImGuiService
{
    /// <summary>
    /// Maps texture pointer handles to weak references of Texture2D objects for lookup by ImGui.
    /// </summary>
    private static readonly Dictionary<IntPtr, WeakReference<Texture2D>> Lookup = new();

    /// <summary>
    /// Reverse mapping from Texture2D objects to their pointer handles for efficient bidirectional lookup.
    /// </summary>
    private static readonly Dictionary<Texture2D, IntPtr> LookupReverse = new();
    
     /// <summary>
    /// Binds a Texture2D to ImGui by creating a pointer handle that can be used in ImGui draw calls.
    /// </summary>
    /// <param name="texture">The texture to bind.</param>
    /// <returns>A pointer handle to the texture, or IntPtr.Zero if the texture is null or disposed.</returns>
    public static IntPtr BindTexture(Texture2D texture)
    {
        if (texture.IsDisposed)
        {
            return IntPtr.Zero;
        }
            
        if (LookupReverse.TryGetValue(texture, out var ptr))
        {
            if (texture.IsDisposed)
            {
                UnbindTexture(texture);
                return IntPtr.Zero;
            }

            return ptr;
        }
            
        ptr = new IntPtr(texture.GetHashCode());
        Lookup[ptr] = new WeakReference<Texture2D>(texture);
        LookupReverse[texture] = ptr;

        return ptr;
    }

    /// <summary>
    /// Removes a texture binding from ImGui.
    /// </summary>
    /// <param name="texture">The texture to unbind.</param>
    /// <returns>True if the texture was found and unbound, false otherwise.</returns>
    public static bool UnbindTexture(Texture2D texture)
    {
        if (!LookupReverse.Remove(texture, out var ptr))
        {
            return false;
        }

        Lookup.Remove(ptr);
        return true;
    }

    /// <summary>
    /// Retrieves a Texture2D from a pointer handle created by <see cref="BindTexture"/>.
    /// </summary>
    /// <param name="ptr">The pointer handle to look up.</param>
    /// <returns>The associated Texture2D, or null if not found or if the texture has been disposed.</returns>
    public static Texture2D? GetBoundTexture(IntPtr ptr)
    {
        if (!Lookup.TryGetValue(ptr, out var weakRef))
        {
            return null;
        }

        if (!weakRef.TryGetTarget(out var texture))
        {
            Lookup.Remove(ptr);
            return null;
        }
            
        if (texture.IsDisposed)
        {
            Lookup.Remove(ptr);
            LookupReverse.Remove(texture);
            return null;
        }

        return texture;
    }

    /// <summary>
    /// Removes texture bindings for textures that have been garbage collected or disposed.
    /// </summary>
    private static void CleanupDeadTextures()
    {
        var deadPtrs = new List<IntPtr>();
        foreach (var kvp in Lookup)
        {
            if (!kvp.Value.TryGetTarget(out var texture) || (texture.IsDisposed))
            {
                deadPtrs.Add(kvp.Key);
            }
        }

        foreach (var ptr in deadPtrs)
        {
            if (Lookup.TryGetValue(ptr, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var texture))
                {
                    LookupReverse.Remove(texture);
                }

                Lookup.Remove(ptr);
            }
        }
    }

}