using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Gem : MonoBehaviour
{
    [SerializeField] float pulseScale = 1.2f;
    [SerializeField] float pulseDuration = 0.5f;

    ISpecialGemEffect _specialGemEffect;
    public ISpecialGemEffect SpecialGemEffect
    {
        get { return _specialGemEffect; }
        set
        {
            if (value != null)
            {
                gemRenderer.color = Color.gray;
            }
            else
            {
                gemRenderer.color = Color.white;
            }

            _specialGemEffect = value;
        }
    }

    GemType _gemType;
    public GemType GemType
    {
        get { return _gemType; }
        set
        {
            gemRenderer.sprite = value.sprite;
            _gemType = value;
        }
    }

    Vector3 originalScale;
    int originalSortOrder;
    SpriteRenderer gemRenderer;

    void Awake()
    {
        originalScale = transform.localScale;
        gemRenderer = GetComponent<SpriteRenderer>();
        gemRenderer.material.SetFloat("_OutlineEnabled", 0);
        originalSortOrder = gemRenderer.sortingOrder;
    }

    void OnEnable()
    {
        SpecialGemEffect = null;
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
