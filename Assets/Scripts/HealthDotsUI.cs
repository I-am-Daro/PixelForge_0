using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthDotsUI : MonoBehaviour
{
    [Header("Bind")]
    [SerializeField] private Health targetHealth;

    [Header("UI")]
    [SerializeField] private Transform container; // ide jönnek a pöttyök (HealthBar)
    [SerializeField] private Image dotPrefab;     // egy Image prefab (UI Image)
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite emptySprite;

    private readonly List<Image> dots = new();

    private void OnEnable()
    {
        if (targetHealth != null)
            targetHealth.OnHealthChanged += Refresh;
    }

    private void OnDisable()
    {
        if (targetHealth != null)
            targetHealth.OnHealthChanged -= Refresh;
    }

    private void Start()
    {
        if (targetHealth == null) return;
        BuildDots(targetHealth.MaxHealth);
        Refresh(targetHealth.CurrentHealth, targetHealth.MaxHealth);
    }

    private void BuildDots(int max)
    {
        // takarítás
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        dots.Clear();

        for (int i = 0; i < max; i++)
        {
            Image dot = Instantiate(dotPrefab, container);
            dot.sprite = fullSprite;
            dots.Add(dot);
        }
    }

    private void Refresh(int current, int max)
    {
        // ha max változik (késõbb shard miatt), újraépítjük
        if (dots.Count != max)
            BuildDots(max);

        for (int i = 0; i < dots.Count; i++)
        {
            dots[i].sprite = (i < current) ? fullSprite : emptySprite;
        }
    }
}
