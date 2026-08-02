using UnityEngine;

namespace YourCat.DesktopPet
{
    public sealed class CatMorphController : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform neck;
        [SerializeField] private Transform[] legs;

        private Vector3 bodyBaseScale;
        private Vector3 headBaseScale;
        private Vector3 neckBaseScale;
        private Vector3[] legBaseScales;

        private void Awake()
        {
            bodyBaseScale = body.localScale;
            headBaseScale = head.localScale;
            neckBaseScale = neck.localScale;
            legBaseScales = new Vector3[legs.Length];

            for (var index = 0; index < legs.Length; index++)
                legBaseScales[index] = legs[index].localScale;
        }

        public void Apply(CatShape shape)
        {
            body.localScale = Vector3.Scale(
                bodyBaseScale,
                new Vector3(
                    Mathf.Lerp(0.85f, 1.2f, shape.weight),
                    Mathf.Lerp(0.94f, 1.08f, shape.weight),
                    Mathf.Lerp(0.9f, 1.14f, shape.weight)
                )
            );

            var faceScale = Mathf.Lerp(0.85f, 1.15f, shape.faceWidth);
            var earScale = Mathf.Lerp(0.8f, 1.2f, shape.earSize);
            head.localScale = Vector3.Scale(headBaseScale, new Vector3(faceScale, earScale, faceScale));
            neck.localScale = Vector3.Scale(neckBaseScale, new Vector3(1f, Mathf.Lerp(0.94f, 1.06f, shape.earSize), 1f));

            var legScale = Mathf.Lerp(0.78f, 1.2f, shape.legLength);
            for (var index = 0; index < legs.Length; index++)
            {
                var scale = legBaseScales[index];
                scale.y *= legScale;
                legs[index].localScale = scale;
            }
        }

        public Vector4 GetVisualSignature()
        {
            return new Vector4(body.localScale.x, head.localScale.x, head.localScale.y, legs[0].localScale.y);
        }
    }
}
