using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public sealed class CommunityLevelBrowser : MonoBehaviour
{
    private const string PlaySceneName = "Levels";
    private const int FeedSortingOrder = 1200;

    private static CommunityLevelBrowser current;

    private string apiBaseUrl;
    private Font font;
    private RectTransform feedContent;
    private GameObject feedRoot;
    private GameObject detailRoot;
    private GameObject composerRoot;
    private Text statusText;
    private Text detailTitleText;
    private Text detailMetaText;
    private Text detailBodyText;
    private RawImage detailMedia;
    private Button detailPlayLevelButton;
    private InputField composerTitleInput;
    private InputField composerAuthorInput;
    private InputField composerBodyInput;
    private Text selectedImageText;
    private string selectedImagePath;
    private CommunityPostDetail openedPost;
    private bool isBusy;
    private LevelSelectCameraScroll cameraScroll;

    public static void Show(string serverUrl)
    {
        if (current != null)
        {
            return;
        }

        GameObject browserObject = new("Community Feed Browser", typeof(RectTransform));
        current = browserObject.AddComponent<CommunityLevelBrowser>();
        current.Initialize(serverUrl);
    }

    private void Initialize(string serverUrl)
    {
        apiBaseUrl = serverUrl;
        // Unity 6 no longer guarantees that the legacy built-in font renders in
        // dynamically-created UGUI canvases. Prefer the Windows runtime font,
        // then retain the built-in fallbacks for other player environments.
        font = Font.CreateDynamicFontFromOSFont("Arial", 24) ??
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
            Resources.GetBuiltinResource<Font>("Arial.ttf");
        cameraScroll = FindFirstObjectByType<LevelSelectCameraScroll>();
        if (cameraScroll != null)
        {
            cameraScroll.enabled = false;
        }

        EnsureEventSystem();
        BuildUi();
        RefreshFeed();
    }

    private void OnDestroy()
    {
        if (cameraScroll != null)
        {
            cameraScroll.enabled = true;
        }

        if (current == this)
        {
            current = null;
        }
    }

    private void BuildUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = FeedSortingOrder;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = GetComponent<RectTransform>();
        CreateImage("Backdrop", root, new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        feedRoot = CreateRootPanel("Community Feed", root);
        detailRoot = CreateRootPanel("Post Detail", root);
        composerRoot = CreateRootPanel("Post Composer", root);
        BuildFeed(feedRoot.GetComponent<RectTransform>());
        BuildDetail(detailRoot.GetComponent<RectTransform>());
        BuildComposer(composerRoot.GetComponent<RectTransform>());
        detailRoot.SetActive(false);
        composerRoot.SetActive(false);
    }

    private GameObject CreateRootPanel(string objectName, RectTransform root)
    {
        Image panel = CreateImage(
            objectName,
            root,
            new Color(0.96f, 0.96f, 0.94f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-760f, -430f),
            new Vector2(760f, 430f));
        AddOutline(panel.gameObject, Color.black, new Vector2(3f, -3f));
        return panel.gameObject;
    }

    private void BuildFeed(RectTransform panel)
    {
        CreateText("Title", panel, "COMMUNITY", 50, TextAnchor.MiddleLeft,
            new Vector2(34f, -80f), new Vector2(520f, -20f), Color.black);
        CreateText("Subtitle", panel, "LEVELS  /  STORIES  /  IMAGES  /  VIDEOS", 20, TextAnchor.MiddleLeft,
            new Vector2(38f, -112f), new Vector2(700f, -82f), new Color(0.25f, 0.25f, 0.25f, 1f));

        Button close = CreateButton("Close", panel, "X", 30, new Vector2(1392f, -76f), new Vector2(1440f, -26f));
        close.onClick.AddListener(Close);
        Button refresh = CreateButton("Refresh", panel, "REFRESH", 21, new Vector2(1242f, -76f), new Vector2(1376f, -26f));
        refresh.onClick.AddListener(RefreshFeed);
        Button create = CreateButton("New Post", panel, "NEW POST", 21, new Vector2(1084f, -76f), new Vector2(1226f, -26f));
        create.onClick.AddListener(OpenComposer);

        statusText = CreateText("Status", panel, "Loading posts...", 20, TextAnchor.MiddleLeft,
            new Vector2(36f, -144f), new Vector2(1040f, -116f), Color.black);

        Image feedBackground = CreateImage("Feed Background", panel, new Color(0.84f, 0.84f, 0.82f, 1f),
            Vector2.zero, Vector2.one, new Vector2(30f, 28f), new Vector2(-30f, -158f));
        AddOutline(feedBackground.gameObject, Color.black, new Vector2(1.5f, -1.5f));

        GameObject viewportObject = new("Feed Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(feedBackground.transform, false);
        viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));

        GameObject contentObject = new("Feed Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport, false);
        feedContent = contentObject.GetComponent<RectTransform>();
        feedContent.anchorMin = new Vector2(0f, 1f);
        feedContent.anchorMax = new Vector2(1f, 1f);
        feedContent.pivot = new Vector2(0.5f, 1f);
        feedContent.anchoredPosition = Vector2.zero;
        feedContent.sizeDelta = Vector2.zero;

        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(14, 14, 14, 14);
        grid.cellSize = new Vector2(690f, 250f);
        grid.spacing = new Vector2(14f, 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = feedBackground.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = feedContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
    }

    private void BuildDetail(RectTransform panel)
    {
        Button back = CreateButton("Back", panel, "BACK", 22, new Vector2(34f, -76f), new Vector2(160f, -28f));
        back.onClick.AddListener(ShowFeed);
        Button close = CreateButton("Close", panel, "X", 30, new Vector2(1392f, -76f), new Vector2(1440f, -26f));
        close.onClick.AddListener(Close);

        detailTitleText = CreateText("Post Title", panel, string.Empty, 43, TextAnchor.MiddleLeft,
            new Vector2(190f, -84f), new Vector2(1180f, -26f), Color.black);
        detailMetaText = CreateText("Post Meta", panel, string.Empty, 21, TextAnchor.MiddleLeft,
            new Vector2(194f, -116f), new Vector2(1200f, -86f), new Color(0.25f, 0.25f, 0.25f, 1f));

        GameObject mediaObject = new("Post Media", typeof(RectTransform), typeof(RawImage));
        mediaObject.transform.SetParent(panel, false);
        detailMedia = mediaObject.GetComponent<RawImage>();
        detailMedia.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        Stretch(detailMedia.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -370f), new Vector2(610f, -140f));
        AddOutline(mediaObject, Color.black, new Vector2(2f, -2f));

        detailPlayLevelButton = CreateButton("Play Attached Level", panel, "PLAY ATTACHED LEVEL", 23,
            new Vector2(36f, -430f), new Vector2(610f, -374f));
        detailPlayLevelButton.onClick.AddListener(PlayOpenedLevel);
        Image bodyBackground = CreateImage("Body Background", panel, new Color(0.86f, 0.86f, 0.84f, 1f),
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(640f, 34f), new Vector2(-36f, -140f));
        AddOutline(bodyBackground.gameObject, Color.black, new Vector2(1.5f, -1.5f));
        GameObject viewportObject = new("Body Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(bodyBackground.transform, false);
        viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -18f));
        GameObject contentObject = new("Body Content", typeof(RectTransform), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        detailBodyText = CreateText("Body", content, string.Empty, 27, TextAnchor.UpperLeft,
            new Vector2(0f, 0f), new Vector2(0f, 0f), Color.black);
        ContentSizeFitter bodyFitter = content.GetComponent<ContentSizeFitter>();
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect bodyScroll = bodyBackground.gameObject.AddComponent<ScrollRect>();
        bodyScroll.viewport = viewport;
        bodyScroll.content = content;
        bodyScroll.horizontal = false;
        bodyScroll.vertical = true;
        bodyScroll.scrollSensitivity = 32f;
    }

    private void BuildComposer(RectTransform panel)
    {
        CreateText("Title", panel, "CREATE IMAGE POST", 46, TextAnchor.MiddleLeft,
            new Vector2(34f, -82f), new Vector2(600f, -24f), Color.black);
        Button cancel = CreateButton("Cancel", panel, "CANCEL", 22, new Vector2(1280f, -76f), new Vector2(1440f, -28f));
        cancel.onClick.AddListener(ShowFeed);

        composerTitleInput = CreateInput("Title", panel, "TITLE", new Vector2(38f, -184f), new Vector2(720f, -128f), false);
        composerAuthorInput = CreateInput("Author", panel, "AUTHOR", new Vector2(38f, -254f), new Vector2(720f, -198f), false);
        Button chooseImage = CreateButton("Choose Image", panel, "CHOOSE IMAGE", 22, new Vector2(38f, -324f), new Vector2(260f, -268f));
        chooseImage.onClick.AddListener(ChooseImage);
        selectedImageText = CreateText("Selected Image", panel, "NO IMAGE SELECTED", 20, TextAnchor.MiddleLeft,
            new Vector2(280f, -322f), new Vector2(900f, -270f), new Color(0.25f, 0.25f, 0.25f, 1f));
        composerBodyInput = CreateInput("Body", panel, "WRITE YOUR POST...", new Vector2(38f, -744f), new Vector2(1440f, -338f), true);
        Button publish = CreateButton("Publish Post", panel, "PUBLISH POST", 25, new Vector2(1160f, -320f), new Vector2(1440f, -264f));
        publish.onClick.AddListener(PublishComposerPost);
        CreateText("Level Hint", panel, "Playable levels are published from the Level Editor.", 21, TextAnchor.MiddleRight,
            new Vector2(760f, -318f), new Vector2(1140f, -270f), new Color(0.25f, 0.25f, 0.25f, 1f));
    }

    private void RefreshFeed()
    {
        if (isBusy)
        {
            return;
        }

        ClearFeedEntries();
        SetBusy(true, "Loading posts...");
        StartCoroutine(CommunityLevelApi.GetPosts(
            apiBaseUrl,
            ShowPosts,
            error => SetBusy(false, "Could not load posts: " + error)));
    }

    private void ShowPosts(CommunityPostListResponse response)
    {
        CommunityPostSummary[] posts = response.posts ?? Array.Empty<CommunityPostSummary>();
        SetBusy(false, posts.Length == 0 ? "No posts yet." : posts.Length + " posts");
        for (int i = 0; i < posts.Length; i++)
        {
            if (posts[i] != null && !string.IsNullOrWhiteSpace(posts[i].id))
            {
                CreateFeedCard(posts[i]);
            }
        }
    }

    private void CreateFeedCard(CommunityPostSummary post)
    {
        Button card = CreateButton("Post - " + post.id, feedContent, string.Empty, 22, Vector2.zero, Vector2.zero);
        Image background = card.GetComponent<Image>();
        background.color = Color.white;
        RectTransform cardRect = card.GetComponent<RectTransform>();
        RawImage media = CreateRawImage("Media", cardRect, new Vector2(12f, -136f), new Vector2(678f, -12f));
        media.color = post.type == "article" ? new Color(0.18f, 0.18f, 0.18f, 1f) : new Color(0.12f, 0.12f, 0.12f, 1f);
        Text mediaLabel = CreateText("Media Label", cardRect, GetTypeLabel(post.type), 24, TextAnchor.MiddleCenter,
            new Vector2(12f, -136f), new Vector2(678f, -12f), Color.white);
        mediaLabel.raycastTarget = false;
        if (post.type == "image" && !string.IsNullOrWhiteSpace(post.mediaUrl))
        {
            StartCoroutine(LoadMonochromeImage(post.mediaUrl, media, mediaLabel));
        }

        string title = string.IsNullOrWhiteSpace(post.title) ? "Untitled Post" : post.title;
        string author = string.IsNullOrWhiteSpace(post.author) ? "Anonymous" : post.author;
        string preview = string.IsNullOrWhiteSpace(post.bodyPreview) ? string.Empty : "\n" + post.bodyPreview;
        Text text = CreateText("Post Text", cardRect,
            title + "\nBY " + author + (post.hasLevel ? "  [PLAYABLE LEVEL]" : string.Empty) + preview,
            20, TextAnchor.UpperLeft, new Vector2(18f, 12f), new Vector2(674f, 132f), Color.black);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = 20;
        card.onClick.AddListener(() => OpenPost(post.id));
    }

    private void OpenPost(string postId)
    {
        if (isBusy)
        {
            return;
        }

        SetBusy(true, "Opening post...");
        StartCoroutine(CommunityLevelApi.GetPost(
            apiBaseUrl,
            postId,
            ShowPostDetail,
            error => SetBusy(false, "Could not open post: " + error)));
    }

    private void ShowPostDetail(CommunityPostDetail post)
    {
        openedPost = post;
        string title = string.IsNullOrWhiteSpace(post.title) ? "Untitled Post" : post.title;
        string author = string.IsNullOrWhiteSpace(post.author) ? "Anonymous" : post.author;
        detailTitleText.text = title;
        detailMetaText.text = GetTypeLabel(post.type) + "  /  BY " + author +
            (post.hasLevel ? "  /  PLAYABLE LEVEL" : string.Empty);
        detailBodyText.text = string.IsNullOrWhiteSpace(post.body) ? "No text." : post.body;
        detailMedia.texture = null;
        detailMedia.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        detailPlayLevelButton.gameObject.SetActive(post.hasLevel && !string.IsNullOrWhiteSpace(post.level));
        if (post.type == "image" && !string.IsNullOrWhiteSpace(post.mediaUrl))
        {
            StartCoroutine(LoadMonochromeImage(post.mediaUrl, detailMedia, null));
        }

        SetBusy(false, string.Empty);
        feedRoot.SetActive(false);
        composerRoot.SetActive(false);
        detailRoot.SetActive(true);
    }

    private void OpenComposer()
    {
        composerTitleInput.SetTextWithoutNotify(string.Empty);
        composerAuthorInput.SetTextWithoutNotify("Anonymous");
        composerBodyInput.SetTextWithoutNotify(string.Empty);
        selectedImagePath = string.Empty;
        selectedImageText.text = "NO IMAGE SELECTED";
        feedRoot.SetActive(false);
        detailRoot.SetActive(false);
        composerRoot.SetActive(true);
    }

    private void PublishComposerPost()
    {
        if (isBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedImagePath))
        {
            SetBusy(false, "Choose an image before publishing.");
            return;
        }

        byte[] imageBytes;
        try
        {
            imageBytes = File.ReadAllBytes(selectedImagePath);
        }
        catch (Exception exception)
        {
            SetBusy(false, "Could not read image: " + exception.Message);
            return;
        }

        SetBusy(true, "Uploading image...");
        StartCoroutine(CommunityLevelApi.UploadImage(
            apiBaseUrl,
            imageBytes,
            selectedImagePath,
            mediaUrl =>
            {
                CommunityPostPublishRequest request = new()
                {
                    type = "image",
                    title = composerTitleInput.text,
                    author = composerAuthorInput.text,
                    body = composerBodyInput.text,
                    mediaUrl = mediaUrl,
                    level = string.Empty
                };
                StartCoroutine(CommunityLevelApi.PublishPost(
                    apiBaseUrl,
                    request,
                    _ =>
                    {
                        SetBusy(false, "Post published.");
                        ShowFeed();
                        RefreshFeed();
                    },
                    error => SetBusy(false, "Publish failed: " + error)));
            },
            error => SetBusy(false, "Image upload failed: " + error)));
    }

    private void PlayOpenedLevel()
    {
        if (openedPost == null || string.IsNullOrWhiteSpace(openedPost.level))
        {
            return;
        }

        if (!LevelJsonUtility.TryParse(openedPost.level, out _, out string error))
        {
            SetBusy(false, "Attached level is invalid: " + error);
            return;
        }

        LevelSceneModeRequest.Clear();
        LevelLoadRequest.Set(openedPost.level, "Community: " + openedPost.title, 0);
        CardSelectionState.PrepareLevelLoad(PlaySceneName);
        SceneTransitionController.LoadScene(PlaySceneName);
        Close();
    }

    private void ShowFeed()
    {
        detailRoot.SetActive(false);
        composerRoot.SetActive(false);
        feedRoot.SetActive(true);
    }

    private void ClearFeedEntries()
    {
        if (feedContent == null)
        {
            return;
        }

        for (int i = feedContent.childCount - 1; i >= 0; i--)
        {
            Destroy(feedContent.GetChild(i).gameObject);
        }
    }

    private void SetBusy(bool value, string status)
    {
        isBusy = value;
        if (statusText != null)
        {
            statusText.text = status;
        }
    }

    private void Close()
    {
        Destroy(gameObject);
    }

    private IEnumerator LoadMonochromeImage(string url, RawImage target, Text label)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success || target == null)
        {
            yield break;
        }

        Texture2D source = DownloadHandlerTexture.GetContent(request);
        if (source == null)
        {
            yield break;
        }

        Texture2D monochrome = new(source.width, source.height, TextureFormat.RGBA32, false);
        Color32[] pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            byte shade = (byte)((pixels[i].r * 30 + pixels[i].g * 59 + pixels[i].b * 11) / 100);
            pixels[i] = new Color32(shade, shade, shade, pixels[i].a);
        }

        monochrome.SetPixels32(pixels);
        monochrome.Apply(false, true);
        target.texture = monochrome;
        target.color = Color.white;
        if (label != null)
        {
            label.gameObject.SetActive(false);
        }
    }

    private static string GetTypeLabel(string type)
    {
        return type switch
        {
            "image" => "IMAGE POST",
            "level" => "PLAYABLE LEVEL",
            _ => "IMAGE POST"
        };
    }

    private void ChooseImage()
    {
        string path = OpenImageFilePicker();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        selectedImagePath = path;
        selectedImageText.text = Path.GetFileName(path);
    }

#if UNITY_STANDALONE_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);
#endif

    private static string OpenImageFilePicker()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Choose Community Image", string.Empty, "png,jpg,jpeg,webp");
#elif UNITY_STANDALONE_WIN
        const int bufferCharacters = 4096;
        IntPtr fileBuffer = Marshal.AllocHGlobal(bufferCharacters * sizeof(char));
        IntPtr filter = Marshal.StringToHGlobalUni("Image Files\0*.png;*.jpg;*.jpeg;*.webp\0\0");
        IntPtr title = Marshal.StringToHGlobalUni("Choose Community Image");
        for (int i = 0; i < bufferCharacters; i++)
        {
            Marshal.WriteInt16(fileBuffer, i * sizeof(char), 0);
        }

        try
        {
            OpenFileName dialog = new()
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                lpstrFilter = filter,
                lpstrFile = fileBuffer,
                nMaxFile = bufferCharacters,
                lpstrTitle = title,
                Flags = 0x00000008 | 0x00000800
            };
            return GetOpenFileName(ref dialog) ? Marshal.PtrToStringUni(fileBuffer) : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(title);
            Marshal.FreeHGlobal(filter);
            Marshal.FreeHGlobal(fileBuffer);
        }
#else
        return string.Empty;
#endif
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new("Community EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private Image CreateImage(string objectName, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject imageObject = new(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        Stretch(image.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
        return image;
    }

    private RawImage CreateRawImage(string objectName, RectTransform parent, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject imageObject = new(objectName, typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(parent, false);
        RawImage image = imageObject.GetComponent<RawImage>();
        SetTopLeftRect(image.rectTransform, offsetMin, offsetMax);
        return image;
    }

    private Text CreateText(string objectName, RectTransform parent, string value, int fontSize, TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        SetTopLeftRect(text.rectTransform, offsetMin, offsetMax);
        return text;
    }

    private Text CreateStretchText(string objectName, RectTransform parent, string value, int fontSize, TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        Text text = CreateText(objectName, parent, value, fontSize, alignment, Vector2.zero, Vector2.zero, color);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, offsetMin, offsetMax);
        return text;
    }

    private Button CreateButton(string objectName, RectTransform parent, string label, int fontSize, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.92f, 0.92f, 0.9f, 1f);
        AddOutline(buttonObject, Color.black, new Vector2(1.5f, -1.5f));
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.9f, 1f);
        colors.highlightedColor = new Color(0.74f, 0.74f, 0.72f, 1f);
        colors.pressedColor = new Color(0.5f, 0.5f, 0.48f, 1f);
        button.colors = colors;
        SetTopLeftRect(buttonObject.GetComponent<RectTransform>(), offsetMin, offsetMax);
        CreateStretchText("Label", buttonObject.GetComponent<RectTransform>(), label, fontSize, TextAnchor.MiddleCenter,
            new Vector2(10f, 4f), new Vector2(-10f, -4f), Color.black);
        return button;
    }

    private InputField CreateInput(string objectName, RectTransform parent, string placeholderValue, Vector2 offsetMin, Vector2 offsetMax, bool multiline)
    {
        GameObject fieldObject = new(objectName + " Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        fieldObject.transform.SetParent(parent, false);
        Image image = fieldObject.GetComponent<Image>();
        image.color = Color.white;
        AddOutline(fieldObject, Color.black, new Vector2(1.5f, -1.5f));
        SetTopLeftRect(fieldObject.GetComponent<RectTransform>(), offsetMin, offsetMax);

        Text text = CreateStretchText("Text", fieldObject.GetComponent<RectTransform>(), string.Empty, 24,
            multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Vector2(14f, 8f), new Vector2(-14f, -8f), Color.black);
        Text placeholder = CreateStretchText("Placeholder", fieldObject.GetComponent<RectTransform>(), placeholderValue, 19,
            multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Vector2(14f, 8f), new Vector2(-14f, -8f), new Color(0.36f, 0.36f, 0.36f, 1f));
        InputField input = fieldObject.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
        return input;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
