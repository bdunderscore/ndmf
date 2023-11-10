namespace nadena.dev.ndmf
{
    /// <summary>
    /// Declares which platforms a plugin or pass will execute for. Plugins and passes which execute for "Generic" will
    /// always execute; otherwise, you can declare a specific platform or platforms to support.
    /// </summary>
    public enum AvatarPlatform
    {
        Generic,
        VRChat,
        UniVRM
    }
}