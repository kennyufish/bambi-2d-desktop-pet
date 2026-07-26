using System;
using UnityEngine;

namespace YourCat.DesktopPet
{
    [Serializable]
    public sealed class CatPackageManifest
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string catId = "";
        public string displayName = "";
        public string modelFile = "cat.glb";
        public string bodyTextureFile = "body.png";
        public string eyeTextureFile = "eyes.png";
        public CatShape shape = new();
    }

    [Serializable]
    public sealed class CatShape
    {
        [Range(0f, 1f)] public float weight = 0.5f;
        [Range(0f, 1f)] public float faceWidth = 0.5f;
        [Range(0f, 1f)] public float earSize = 0.5f;
        [Range(0f, 1f)] public float legLength = 0.5f;
    }
}
