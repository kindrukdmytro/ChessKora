using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ChessPieceUI : MonoBehaviour
{
    private Image pieceImage;

    public PieceType Type { get; private set; }
    public PieceColor Color { get; private set; }

    private void Awake()
    {
        pieceImage = GetComponent<Image>();
    }

    public void Initialize(PieceType type, PieceColor color, Sprite sprite)
    {
        Type = type;
        Color = color;

        if (pieceImage == null)
            pieceImage = GetComponent<Image>();

        pieceImage.sprite = sprite;
        pieceImage.preserveAspect = true;
        pieceImage.color = UnityEngine.Color.white;
        pieceImage.raycastTarget = false;
    }
}