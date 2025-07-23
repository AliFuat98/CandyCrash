using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Gem : MonoBehaviour
{
    [SerializeField] float pulseScale = 1.2f;
    [SerializeField] float pulseDuration = 0.5f;

    GemType _gemType;
    public GemType GemType
    {
        get { return _gemType; }
        set
        {
            GetComponent<SpriteRenderer>().sprite = value.sprite;
            _gemType = value;
        }
    }
    Vector3 originalScale;
    int originalSortOrder;
    Renderer gemRenderer;

    void Awake()
    {
        originalScale = transform.localScale;
        gemRenderer = GetComponent<Renderer>();
        gemRenderer.material.SetFloat("_OutlineEnabled", 0);
        originalSortOrder = gemRenderer.sortingOrder;
    }

    public void DestroyGem()
    {
        ObjectPoolManager.ReturnObjectToPool(gameObject);
    }

    public void OnSelect()
    {
        gemRenderer.material.SetFloat("_OutlineEnabled", 1);
        gemRenderer.sortingOrder = originalSortOrder + 1;

        transform.DOScale(originalScale * pulseScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void OnDeselect()
    {
        gemRenderer.material.SetFloat("_OutlineEnabled", 0);
        gemRenderer.sortingOrder = originalSortOrder;

        DOTween.Kill(transform);
        transform.localScale = originalScale;
    }
}