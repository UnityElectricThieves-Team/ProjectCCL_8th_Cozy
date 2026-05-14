using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InputInteractionManager : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask interactableLayerMask = ~0;
    [SerializeField] private int maxOverlapHits = 5;
    [SerializeField] private bool debugLogs;
    [Tooltip("When enabled, skips Physics2D overlap while the mouse pixel position is unchanged and no mouse button was pressed this frame. Turn off if interactables can move under a stationary cursor.")]
    [SerializeField] private bool _skipRescanWhenPointerUnchanged = true;

    private Collider2D[] hitBuffer;
    private IHoverable currentHover;
    private IClickable currentClickable;
    private IShiftRightClickable currentShiftRightClickable;
    private readonly Dictionary<Collider2D, CachedInteractable> interactableCache = new Dictionary<Collider2D, CachedInteractable>(256);

    private Vector2Int _lastPointerPixel;
    private bool _hasLastPointerPixel;

    private struct CachedInteractable
    {
        public IHoverable hoverable;
        public IClickable clickable;
        public IShiftRightClickable shiftRightClickable;
        public Renderer renderer;
        public int sortingOrder;
        public int sortingLayerValue;
    }

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        maxOverlapHits = Mathf.Max(1, maxOverlapHits);
        hitBuffer = new Collider2D[maxOverlapHits];
    }

    private void OnDisable()
    {
        interactableCache.Clear();
        _hasLastPointerPixel = false;
    }

    private void Update()
    {
        if (targetCamera == null || Mouse.current == null)
        {
            return;
        }

        var mouse = Mouse.current;
        var pointerPixel = Vector2Int.FloorToInt(mouse.position.ReadValue());
        var pointerMoved = !_hasLastPointerPixel || pointerPixel != _lastPointerPixel;
        var anyMousePress = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;

        IHoverable nextHover;
        IClickable nextClickable;
        IShiftRightClickable nextShiftRightClickable;

        if (_skipRescanWhenPointerUnchanged && _hasLastPointerPixel && !pointerMoved && !anyMousePress)
        {
            nextHover = currentHover;
            nextClickable = currentClickable;
            nextShiftRightClickable = currentShiftRightClickable;
        }
        else
        {
            _lastPointerPixel = pointerPixel;
            _hasLastPointerPixel = true;

            var mouseWorld = targetCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            var point2D = new Vector2(mouseWorld.x, mouseWorld.y);

            var hitCount = Physics2D.OverlapPointNonAlloc(point2D, hitBuffer, interactableLayerMask);
            nextHover = null;
            nextClickable = null;
            nextShiftRightClickable = null;
            var bestSortingOrder = int.MinValue;
            var bestSortingLayerValue = int.MinValue;

            for (var i = 0; i < hitCount; i++)
            {
                var collider = hitBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                var cached = GetOrCreateCachedInteractable(collider);
                var candidateHover = cached.hoverable;
                var candidateClickable = cached.clickable;
                var candidateShiftRightClickable = cached.shiftRightClickable;
                if (candidateHover == null && candidateClickable == null && candidateShiftRightClickable == null)
                {
                    continue;
                }

                var candidateSortingOrder = cached.sortingOrder;
                var candidateSortingLayerValue = cached.sortingLayerValue;

                var isBetter = candidateSortingLayerValue > bestSortingLayerValue
                               || (candidateSortingLayerValue == bestSortingLayerValue && candidateSortingOrder > bestSortingOrder);

                if (!isBetter)
                {
                    continue;
                }

                bestSortingLayerValue = candidateSortingLayerValue;
                bestSortingOrder = candidateSortingOrder;
                nextHover = candidateHover;
                nextClickable = candidateClickable;
                nextShiftRightClickable = candidateShiftRightClickable;
            }
        }

        if (!ReferenceEquals(nextHover, currentHover))
        {
            currentHover?.OnHoverExit();
            currentHover = nextHover;
            currentHover?.OnHoverEnter();

            if (debugLogs)
            {
                Debug.Log($"[InputInteractionManager] Hover target changed: {(currentHover == null ? "none" : currentHover.ToString())}");
            }
        }

        currentClickable = nextClickable;
        currentShiftRightClickable = nextShiftRightClickable;

        if (currentClickable != null && mouse.leftButton.wasPressedThisFrame)
        {
            currentClickable.OnClick();
        }

        var keyboard = Keyboard.current;
        if (currentShiftRightClickable != null
            && mouse.rightButton.wasPressedThisFrame
            && keyboard != null
            && keyboard.shiftKey.isPressed)
        {
            currentShiftRightClickable.OnShiftRightClick();
        }
    }

    private CachedInteractable GetOrCreateCachedInteractable(Collider2D collider)
    {
        if (interactableCache.TryGetValue(collider, out var cached))
        {
            return cached;
        }

        var renderer = collider.GetComponent<Renderer>();
        cached = new CachedInteractable
        {
            hoverable = collider.GetComponent<IHoverable>(),
            clickable = collider.GetComponent<IClickable>(),
            shiftRightClickable = collider.GetComponent<IShiftRightClickable>(),
            renderer = renderer,
            sortingOrder = renderer != null ? renderer.sortingOrder : 0,
            sortingLayerValue = renderer != null ? SortingLayer.GetLayerValueFromID(renderer.sortingLayerID) : 0
        };

        interactableCache[collider] = cached;
        return cached;
    }
}
