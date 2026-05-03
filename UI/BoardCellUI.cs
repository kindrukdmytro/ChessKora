using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image), typeof(Button))]
public class BoardCellUI : MonoBehaviour
{
    public int X { get; private set; }
    public int Y { get; private set; }

    private Image cellImage;
    private Button cellButton;
    private BoardUIManager boardUIManager;
    private Color baseColor;

    private void Awake()
    {
        cellImage = GetComponent<Image>();
        cellButton = GetComponent<Button>();
    }

    public void Initialize(int boardX, int boardY, Color color, BoardUIManager manager)
    {
        X = boardX;
        Y = boardY;
        boardUIManager = manager;
        baseColor = color;

        if (cellImage == null)
            cellImage = GetComponent<Image>();

        if (cellButton == null)
            cellButton = GetComponent<Button>();

        cellImage.color = baseColor;

        cellButton.onClick.RemoveAllListeners();
        cellButton.onClick.AddListener(OnCellClicked);
    }

    private void OnCellClicked()
    {
        boardUIManager.OnCellClicked(X, Y);
    }

    public void ResetColor()
    {
        if (cellImage == null)
            cellImage = GetComponent<Image>();

        cellImage.color = baseColor;
    }

    public void Highlight(Color highlightColor)
    {
        if (cellImage == null)
            cellImage = GetComponent<Image>();

        cellImage.color = highlightColor;
    }
}