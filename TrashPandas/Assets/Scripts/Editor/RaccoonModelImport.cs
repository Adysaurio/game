using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TrashPandas.EditorTools
{
    /// <summary>
    /// Import settings for the Meshy raccoon (art/raccoon → tools/raccoon_to_fbx.py → Assets/Art/Raccoon/Raccoon.fbx):
    /// generic rig, our own URP material, and the looping clips marked to loop.
    /// </summary>
    public sealed class RaccoonModelImport : AssetPostprocessor
    {
        public const string ModelPath = "Assets/Art/Raccoon/Raccoon.fbx";
        static readonly string[] Loops = { "Idle", "Walk", "Sneak", "Crawl", "Run", "Push", "Hang", "Carry", "Dance" };

        void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.globalScale = 1f;
            mi.importCameras = mi.importLights = false;
        }

        void OnPreprocessAnimation()
        {
            if (assetPath != ModelPath) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                // Takes come in as "Armature|Walk" or "Walk".
                string name = c.takeName.Contains("|") ? c.takeName.Split('|').Last() : c.takeName;
                c.name = name;
                c.loopTime = Loops.Contains(name);
                c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = false;
            }
            mi.clipAnimations = clips;
        }
    }
}
