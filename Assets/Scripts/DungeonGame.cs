using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DungeonGame : MonoBehaviour
{
    private enum ItemType { None, Key, Bomb }
    private enum GameState { Playing, Won, Lost }

    private const int Size = 5;
    private readonly ItemType[,] items = new ItemType[Size, Size];
    private readonly bool[,] explored = new bool[Size, Size];
    private int playerX;
    private int playerY;
    private bool hasKey;
    private GameState state;

    private Text statusText;
    private Text keyText;
    private Text detailText;
    private Button searchButton;
    private Button exitButton;
    private Button restartButton;
    private RectTransform gridRoot;
    private readonly Button[] roomButtons = new Button[Size * Size];
    private Font uiFont;

    private static readonly Color FloorColor = new Color(0.16f, 0.21f, 0.28f);
    private static readonly Color ExploredColor = new Color(0.27f, 0.34f, 0.40f);
    private static readonly Color PlayerColor = new Color(0.20f, 0.72f, 0.65f);
    private static readonly Color ExitColor = new Color(0.82f, 0.67f, 0.28f);

    private void Start()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildInterface();
        RestartGame();
    }

    private void Update()
    {
        if (state != GameState.Playing) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector2Int move = Vector2Int.zero;
        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) move.y = 1;
        else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) move.y = -1;
        else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) move.x = -1;
        else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) move.x = 1;
        if (move != Vector2Int.zero) MovePlayer(move.x, move.y);
        if (keyboard.spaceKey.wasPressedThisFrame) SearchCurrentRoom();
    }

    private void BuildInterface()
    {
        var canvasObject = new GameObject("Game UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1100, 760);
        scaler.matchWidthOrHeight = 0.5f;
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        var background = MakePanel(canvas.transform, "Background", new Color(0.055f, 0.075f, 0.105f));
        Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var title = MakeText(background.transform, "Title", "THE UNKNOWN ROOMS", 32, TextAnchor.MiddleCenter, Color.white);
        Place(title.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f), Vector2.zero, Vector2.zero);
        statusText = MakeText(background.transform, "Status", "", 21, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.96f));
        Place(statusText.rectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);

        gridRoot = MakePanel(background.transform, "Room Grid", new Color(0.09f, 0.12f, 0.17f)).GetComponent<RectTransform>();
        Place(gridRoot, new Vector2(0.12f, 0.16f), new Vector2(0.66f, 0.76f), Vector2.zero, Vector2.zero);
        var layout = gridRoot.gameObject.AddComponent<GridLayoutGroup>();
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Size;
        layout.spacing = new Vector2(7, 7);
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.cellSize = new Vector2(90, 82);
        for (int y = Size - 1; y >= 0; y--)
        for (int x = 0; x < Size; x++)
        {
            int roomX = x, roomY = y;
            var button = MakeButton(gridRoot, "Room " + (x + 1) + "," + (y + 1), "？", 22, FloorColor);
            button.onClick.AddListener(() => MoveToRoom(roomX, roomY));
            roomButtons[ButtonIndex(x, y)] = button;
        }

        var side = MakePanel(background.transform, "Controls", new Color(0.09f, 0.12f, 0.17f));
        Place(side.GetComponent<RectTransform>(), new Vector2(0.70f, 0.16f), new Vector2(0.94f, 0.76f), Vector2.zero, Vector2.zero);
        keyText = MakeText(side.transform, "Key Status", "鍵：なし", 20, TextAnchor.MiddleCenter, Color.white);
        Place(keyText.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
        detailText = MakeText(side.transform, "Room Detail", "部屋を選んで移動", 19, TextAnchor.MiddleCenter, new Color(0.80f, 0.86f, 0.92f));
        Place(detailText.rectTransform, new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
        searchButton = MakeButton(side.transform, "Search Button", "この部屋を探索", 18, new Color(0.25f, 0.54f, 0.65f));
        Place(searchButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.54f), Vector2.zero, Vector2.zero);
        searchButton.onClick.AddListener(SearchCurrentRoom);
        exitButton = MakeButton(side.transform, "Exit Button", "脱出する", 18, new Color(0.52f, 0.40f, 0.20f));
        Place(exitButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.37f), Vector2.zero, Vector2.zero);
        exitButton.onClick.AddListener(AttemptExit);
        restartButton = MakeButton(side.transform, "Restart Button", "最初から", 17, new Color(0.32f, 0.37f, 0.45f));
        Place(restartButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.20f), Vector2.zero, Vector2.zero);
        restartButton.onClick.AddListener(RestartGame);
        var help = MakeText(background.transform, "Help", "WASD / 矢印キー：移動　　Space：探索　　開始：左下　／　出口：右上", 15, TextAnchor.MiddleCenter, new Color(0.57f, 0.65f, 0.74f));
        Place(help.rectTransform, new Vector2(0.05f, 0.055f), new Vector2(0.95f, 0.12f), Vector2.zero, Vector2.zero);
    }

    private void RestartGame()
    {
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++) { items[x, y] = ItemType.None; explored[x, y] = false; }
        // Contents are fixed, but stay hidden until the room is searched.
        PlaceItem(3, 3, ItemType.Key);
        PlaceItem(2, 4, ItemType.Bomb);
        PlaceItem(4, 2, ItemType.Bomb);
        playerX = 0; playerY = 0; hasKey = false; state = GameState.Playing;
        explored[playerX, playerY] = true;
        detailText.text = "開始の部屋\n中身：空";
        statusText.text = "鍵を探して出口へ向かおう";
        RefreshUI();
    }

    private void PlaceItem(int x, int y, ItemType item)
    {
        if (x >= 1 && x <= Size && y >= 1 && y <= Size && !(x == 1 && y == 1) && !(x == Size && y == Size))
            items[x - 1, y - 1] = item;
    }

    private void MovePlayer(int dx, int dy)
    {
        int x = playerX + dx, y = playerY + dy;
        if (x >= 0 && x < Size && y >= 0 && y < Size) MoveToRoom(x, y);
    }

    private void MoveToRoom(int x, int y)
    {
        if (state != GameState.Playing || Mathf.Abs(playerX - x) + Mathf.Abs(playerY - y) != 1) return;
        playerX = x; playerY = y;
        detailText.text = explored[x, y] ? DescribeRoom(x, y) : "未探索の部屋\n中身はまだ分からない";
        statusText.text = (x == Size - 1 && y == Size - 1) ? "出口の部屋に着いた" : "部屋に入った。探索するか、別の部屋へ移動しよう";
        RefreshUI();
    }

    private void SearchCurrentRoom()
    {
        if (state != GameState.Playing) return;
        if (explored[playerX, playerY]) { statusText.text = "この部屋はもう探索済み"; return; }
        explored[playerX, playerY] = true;
        switch (items[playerX, playerY])
        {
            case ItemType.Key:
                hasKey = true; detailText.text = "鍵を見つけた！"; statusText.text = "鍵を手に入れた。出口を目指そう"; break;
            case ItemType.Bomb:
                detailText.text = "爆弾を見つけた…"; statusText.text = "爆弾だ！ゲームオーバー"; state = GameState.Lost; break;
            default:
                detailText.text = "この部屋は空だった"; statusText.text = "何も見つからなかった"; break;
        }
        RefreshUI();
    }

    private void AttemptExit()
    {
        if (state != GameState.Playing) return;
        if (playerX != Size - 1 || playerY != Size - 1) { statusText.text = "出口の部屋（右上）まで移動しよう"; return; }
        if (!hasKey) { statusText.text = "鍵がかかっている。鍵を探そう"; detailText.text = "出口\n鍵が必要"; return; }
        state = GameState.Won; statusText.text = "脱出成功！おめでとう！"; detailText.text = "鍵で扉を開けた\n脱出成功！"; RefreshUI();
    }

    private string DescribeRoom(int x, int y)
    {
        if (x == Size - 1 && y == Size - 1) return "出口\n" + (hasKey ? "鍵が開く" : "鍵が必要");
        switch (items[x, y])
        {
            case ItemType.Key: return "探索済み\n鍵を発見";
            case ItemType.Bomb: return "探索済み\n爆弾";
            default: return "探索済み\n空";
        }
    }

    private void RefreshUI()
    {
        if (statusText == null) return;
        keyText.text = hasKey ? "鍵：あり" : "鍵：なし";
        searchButton.interactable = state == GameState.Playing && !explored[playerX, playerY];
        exitButton.interactable = state == GameState.Playing && playerX == Size - 1 && playerY == Size - 1;
        restartButton.interactable = true;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            var button = roomButtons[ButtonIndex(x, y)];
            var label = button.GetComponentInChildren<Text>();
            bool isPlayer = x == playerX && y == playerY, isExit = x == Size - 1 && y == Size - 1;
            if (isPlayer) label.text = "●\nあなた";
            else if (isExit) label.text = explored[x, y] ? "出口" : "出口\n？";
            else if (!explored[x, y]) label.text = "？";
            else if (items[x, y] == ItemType.Key) label.text = "鍵";
            else if (items[x, y] == ItemType.Bomb) label.text = "爆弾";
            else label.text = "空";
            var colors = button.colors;
            colors.normalColor = isPlayer ? PlayerColor : (isExit ? ExitColor : (explored[x, y] ? ExploredColor : FloorColor));
            colors.highlightedColor = colors.normalColor * 1.2f;
            button.colors = colors;
            button.interactable = state == GameState.Playing;
        }
    }

    private int ButtonIndex(int x, int y) => (Size - 1 - y) * Size + x;
    private GameObject MakePanel(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false); obj.GetComponent<Image>().color = color; return obj;
    }
    private Text MakeText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<Text>(); text.font = uiFont; text.text = value; text.fontSize = size; text.alignment = alignment; text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; return text;
    }
    private Button MakeButton(Transform parent, string name, string label, int size, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color; var button = obj.GetComponent<Button>();
        var text = MakeText(obj.transform, "Label", label, size, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(5, 3), new Vector2(-5, -3)); return button;
    }
    private void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
    private void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Place(rect, anchorMin, anchorMax, offsetMin, offsetMax);
    }
}
