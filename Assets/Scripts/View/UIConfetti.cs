using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Archer.Scripts.View
{
    /// <summary>
    /// A burst of paper confetti made of pooled UI Images, for the new-best celebration. Plain
    /// Images rather than a ParticleSystem so it works on any canvas render mode and sorts with
    /// the panel it belongs to. Unscaled time: it plays on the Game Over screen, where the game
    /// is paused.
    /// </summary>
    public class UIConfetti : MonoBehaviour
    {
        public Sprite pieceSprite;
        public int poolSize = 48;
        public Color[] colours =
        {
            new Color32(0xE8, 0x54, 0x3F, 0xFF),
            new Color32(0xFF, 0xC5, 0x3D, 0xFF),
            new Color32(0x7E, 0xD9, 0x57, 0xFF),
            new Color32(0x6E, 0xC6, 0xF5, 0xFF),
            new Color32(0xF5, 0x6F, 0xA8, 0xFF),
            new Color32(0x9B, 0x5D, 0xE5, 0xFF),
        };

        private readonly List<Image> pieces = new List<Image>();
        private readonly List<Tween> running = new List<Tween>();

        private void EnsurePool()
        {
            if (pieces.Count > 0) return;

            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = new GameObject("Piece", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                Image image = go.AddComponent<Image>();
                image.sprite = pieceSprite;
                image.raycastTarget = false;
                go.SetActive(false);
                pieces.Add(image);
            }
        }

        /// <summary>Throw the confetti up and out from this object's position.</summary>
        public void Burst()
        {
            EnsurePool();
            Stop();

            for (int i = 0; i < pieces.Count; i++)
            {
                Image piece = pieces[i];
                RectTransform rt = piece.rectTransform;

                piece.gameObject.SetActive(true);
                piece.color = colours[Random.Range(0, colours.Length)];
                rt.sizeDelta = new Vector2(Random.Range(14f, 26f), Random.Range(22f, 40f));
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                rt.localScale = Vector3.one;

                // Fan upward, then fall well past where it started.
                float angle = Random.Range(25f, 155f) * Mathf.Deg2Rad;
                float reach = Random.Range(260f, 620f);
                Vector2 peak = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                float rise = Random.Range(0.3f, 0.45f);
                float fall = Random.Range(0.9f, 1.5f);
                float drift = peak.x * Random.Range(1.25f, 1.6f);

                Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(piece.gameObject);
                seq.Append(rt.DOAnchorPosY(peak.y, rise).SetEase(Ease.OutQuad));
                seq.Append(rt.DOAnchorPosY(peak.y - Random.Range(700f, 1100f), fall).SetEase(Ease.InQuad));
                seq.Insert(0f, rt.DOAnchorPosX(drift, rise + fall).SetEase(Ease.OutQuad));
                seq.Insert(0f, rt.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-720f, 720f)), rise + fall,
                    RotateMode.FastBeyond360).SetEase(Ease.Linear));
                // A flutter: flattening on one axis reads as the paper turning over.
                seq.Insert(0f, rt.DOScaleX(0.15f, Random.Range(0.18f, 0.3f)).SetLoops(8, LoopType.Yoyo));
                seq.Insert(rise + fall * 0.6f, piece.DOFade(0f, fall * 0.4f));
                seq.OnComplete(() => piece.gameObject.SetActive(false));
                running.Add(seq);
            }
        }

        public void Stop()
        {
            foreach (Tween tween in running)
                tween?.Kill();
            running.Clear();

            foreach (Image piece in pieces)
            {
                if (piece != null) piece.gameObject.SetActive(false);
            }
        }

        void OnDisable() => Stop();
    }
}
