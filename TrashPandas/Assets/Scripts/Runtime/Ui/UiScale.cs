using UnityEngine;

namespace TrashPandas.Runtime.Ui
{
    /// <summary>
    /// Resolution-independent IMGUI: lays the greybox UI out on a 720-px-tall virtual canvas and scales it
    /// to the window, so a maximized or Retina window doesn't make everything tiny.
    /// </summary>
    public static class UiScale
    {
        public const float ReferenceHeight = 720f;

        public static float ScaleFor(int screenHeight) => Mathf.Max(1f, screenHeight / ReferenceHeight);

        public static float Current => ScaleFor(Screen.height);
        /// <summary>Virtual canvas size to lay out against (use instead of Screen.width/height in OnGUI).</summary>
        public static float Width => Screen.width / Current;
        public static float Height => Screen.height / Current;

        /// <summary>Call at the top of OnGUI.</summary>
        public static void Apply() => GUI.matrix = Matrix4x4.Scale(new Vector3(Current, Current, 1f));

        /// <summary>A screen-space point (e.g. from WorldToScreenPoint, origin bottom-left) on the virtual canvas (origin top-left).</summary>
        public static Vector2 FromScreen(Vector3 screenPoint) => new Vector2(screenPoint.x / Current, Height - screenPoint.y / Current);
    }
}
