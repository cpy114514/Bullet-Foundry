using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

public sealed class CommunityLevelBrowser : MonoBehaviour
{
    private const string PlaySceneName = "Levels";
    private const string LevelSelectSceneName = "LevelSelect";
    private const string CommunitySceneName = "Community";
    private static readonly Dictionary<string, Texture2D> monochromeImageCache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> imageLoadsInProgress = new(StringComparer.Ordinal);

    [SerializeField] private string apiBaseUrl;
    [SerializeField] private Font font;
    [SerializeField] private RectTransform feedContent;
    [SerializeField] private RectTransform myPostsContent;
    [SerializeField] private RectTransform favoritesContent;
    [SerializeField] private RectTransform commentContent;
    [SerializeField] private GameObject feedRoot;
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private GameObject composerRoot;
    [SerializeField] private GameObject loginRoot;
    [SerializeField] private GameObject myPostsRoot;
    [SerializeField] private GameObject favoritesRoot;
    [SerializeField] private Text statusText;
    [SerializeField] private Text accountText;
    [SerializeField] private Text detailTitleText;
    [SerializeField] private Text detailMetaText;
    [SerializeField] private Text detailBodyText;
    [SerializeField] private RawImage detailMedia;
    [SerializeField] private Text detailMediaStatus;
    [SerializeField] private RectTransform detailPostContent;
    [SerializeField] private RectTransform detailMediaFrame;
    [SerializeField] private RectTransform detailBodyFrame;
    [SerializeField] private RectTransform detailCommentsHeading;
    [SerializeField] private RectTransform detailCommentsFrame;
    [SerializeField] private RectTransform detailCommentInputFrame;
    [SerializeField] private RectTransform detailCommentButtonFrame;
    [SerializeField] private RectTransform detailCommentStatusFrame;
    [SerializeField] private Button detailPlayLevelButton;
    [SerializeField] private Button detailLikeButton;
    [SerializeField] private Button detailFavoriteButton;
    [SerializeField] private Button detailShareButton;
    [SerializeField] private InputField composerTitleInput;
    [SerializeField] private InputField composerBodyInput;
    [SerializeField] private Text composerAccountText;
    [SerializeField] private Text selectedImageText;
    [SerializeField] private Text selectedLevelText;
    [SerializeField] private InputField loginUsernameInput;
    [SerializeField] private InputField loginPasswordInput;
    [SerializeField] private Text loginStatusText;
    [SerializeField] private InputField commentInput;
    [SerializeField] private Text commentStatusText;

    private string selectedImagePath;
    private string selectedLevelJson;
    private string editingPostId;
    private string editingMediaUrl;
    private string editingLevelJson;
    private CommunityPostDraft pendingDraft;
    private CommunityPostDetail openedPost;
    private float detailMediaAspectRatio = 16f / 9f;
    private Coroutine detailLayoutRoutine;
    private bool isBusy;

    public static void Show(string serverUrl)
    {
        CommunitySceneRequest.Open(serverUrl);
        SceneTransitionController.LoadScene(CommunitySceneName);
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            Initialize(CommunitySceneRequest.Consume(apiBaseUrl), CommunitySceneRequest.ConsumeDraft());
        }
    }

    private void Initialize(string serverUrl, CommunityPostDraft draft)
    {
        apiBaseUrl = serverUrl;
        pendingDraft = draft;
        if (font == null || feedContent == null || favoritesContent == null || feedRoot == null || detailRoot == null || composerRoot == null || loginRoot == null || myPostsRoot == null || favoritesRoot == null)
        {
            Debug.LogError("Community scene is missing prebuilt UI references.", this);
            enabled = false;
            return;
        }
        BindSceneButtons();
        ShowFeed();
        UpdateAccountUi();
        RefreshFeed();
        if (pendingDraft != null)
        {
            if (CommunityAccount.IsSignedIn)
            {
                OpenComposerWithDraft(pendingDraft);
            }
            else
            {
                OpenLogin();
            }
        }
        if (CommunityAccount.IsSignedIn)
        {
            StartCoroutine(CommunityLevelApi.GetMe(apiBaseUrl, response => { CommunityAccount.Set(new CommunityAuthResponse { username = response.username, token = CommunityAccount.Token }); UpdateAccountUi(); }, _ => { CommunityAccount.Clear(); UpdateAccountUi(); }));
        }
    }

    private void BindSceneButtons()
    {
        BindButton("Community Feed/Close", Close);
        BindButton("Community Feed/Refresh", RefreshFeed);
        BindButton("Community Feed/New Post", OpenComposer);
        BindButton("Community Feed/My Posts", OpenMyPosts);
        BindButton("Community Feed/Favorites", OpenFavorites);
        BindButton("Community Feed/Login", OpenLogin);
        BindButton("Post Detail/Back", ShowFeed);
        BindButton("Post Detail/Close", Close);
        BindButton("Post Detail/Post Comment", PublishComment);
        BindButton("Post Composer/Cancel", ShowFeed);
        BindButton("Post Composer/Choose Image", ChooseImage);
        BindButton("Post Composer/Attach Level", ChooseLevel);
        BindButton("Post Composer/Publish Post", PublishComposerPost);
        BindButton("Login/Register", Register);
        BindButton("Login/Login", Login);
        BindButton("Login/Cancel", ShowFeed);
        BindButton("My Posts/Back", ShowFeed);
        BindButton("My Posts/Close", Close);
        BindButton("Favorites/Back", ShowFeed);
        BindButton("Favorites/Close", Close);
        if (detailPlayLevelButton != null) detailPlayLevelButton.onClick.AddListener(PlayOpenedLevel);
        if (detailLikeButton != null) detailLikeButton.onClick.AddListener(ToggleOpenedPostLike);
        if (detailFavoriteButton != null) detailFavoriteButton.onClick.AddListener(ToggleOpenedPostFavorite);
        if (detailShareButton != null) detailShareButton.onClick.AddListener(ShareOpenedPost);
    }

    private void BindButton(string path, UnityAction callback)
    {
        Transform target = transform.Find(path);
        if (target == null || !target.TryGetComponent(out Button button)) { Debug.LogError("Community scene is missing button: " + path, this); return; }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(callback);
    }

    [ContextMenu("Build Community Scene UI")]
    public void BuildSceneUiForEditor()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        Canvas canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1200;
        CanvasScaler scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        RectTransform root = GetComponent<RectTransform>();
        CreateImage("Backdrop", root, new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        feedRoot = CreateRootPanel("Community Feed", root);
        detailRoot = CreateRootPanel("Post Detail", root);
        composerRoot = CreateRootPanel("Post Composer", root);
        loginRoot = CreateRootPanel("Login", root);
        myPostsRoot = CreateRootPanel("My Posts", root);
        favoritesRoot = CreateRootPanel("Favorites", root);
        BuildFeed(feedRoot.GetComponent<RectTransform>());
        BuildDetail(detailRoot.GetComponent<RectTransform>());
        BuildComposer(composerRoot.GetComponent<RectTransform>());
        BuildLogin(loginRoot.GetComponent<RectTransform>());
        BuildMyPosts(myPostsRoot.GetComponent<RectTransform>());
        BuildFavorites(favoritesRoot.GetComponent<RectTransform>());
        detailRoot.SetActive(false); composerRoot.SetActive(false); loginRoot.SetActive(false); myPostsRoot.SetActive(false); favoritesRoot.SetActive(false);
    }

    private GameObject CreateRootPanel(string objectName, RectTransform root)
    {
        Image panel = CreateImage(objectName, root, new Color(0.96f, 0.96f, 0.94f, 1f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-760f, -430f), new Vector2(760f, 430f));
        AddOutline(panel.gameObject, Color.black, new Vector2(3f, -3f)); return panel.gameObject;
    }

    private void BuildFeed(RectTransform panel)
    {
        CreateText("Title", panel, "COMMUNITY", 50, TextAnchor.MiddleLeft, new Vector2(34f, -80f), new Vector2(520f, -20f), Color.black);
        CreateText("Subtitle", panel, "POSTS  /  LEVELS  /  COMMENTS", 20, TextAnchor.MiddleLeft, new Vector2(38f, -112f), new Vector2(700f, -82f), new Color(.25f, .25f, .25f, 1f));
        CreateButton("Close", panel, "X", 30, new Vector2(1392f, -76f), new Vector2(1440f, -26f));
        CreateButton("Refresh", panel, "REFRESH", 19, new Vector2(1242f, -76f), new Vector2(1376f, -26f));
        CreateButton("New Post", panel, "NEW POST", 19, new Vector2(1084f, -76f), new Vector2(1226f, -26f));
        CreateButton("My Posts", panel, "MY POSTS", 19, new Vector2(906f, -76f), new Vector2(1068f, -26f));
        CreateButton("Favorites", panel, "SAVED", 19, new Vector2(728f, -76f), new Vector2(890f, -26f));
        CreateButton("Login", panel, "LOGIN", 19, new Vector2(566f, -76f), new Vector2(712f, -26f));
        accountText = CreateText("Account", panel, "NOT SIGNED IN", 18, TextAnchor.MiddleRight, new Vector2(740f, -112f), new Vector2(1438f, -82f), new Color(.25f, .25f, .25f, 1f));
        statusText = CreateText("Status", panel, "Loading posts...", 20, TextAnchor.MiddleLeft, new Vector2(36f, -144f), new Vector2(1040f, -116f), Color.black);
        Image background = CreateImage("Feed Background", panel, new Color(.84f, .84f, .82f, 1f), Vector2.zero, Vector2.one, new Vector2(30f, 28f), new Vector2(-30f, -158f)); AddOutline(background.gameObject, Color.black, new Vector2(1.5f, -1.5f));
        feedContent = CreateGridScroll("Feed", background.GetComponent<RectTransform>(), new Vector2(10f, 10f), new Vector2(-10f, -10f));
    }

    private void BuildDetail(RectTransform panel)
    {
        CreateButton("Back", panel, "BACK", 21, new Vector2(34f, -76f), new Vector2(160f, -28f));
        CreateButton("Close", panel, "X", 30, new Vector2(1392f, -76f), new Vector2(1440f, -26f));
        detailTitleText = CreateText("Post Title", panel, string.Empty, 32, TextAnchor.MiddleLeft, new Vector2(190f, -84f), new Vector2(800f, -26f), Color.black);
        detailTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        detailTitleText.resizeTextForBestFit = true;
        detailTitleText.resizeTextMinSize = 20;
        detailTitleText.resizeTextMaxSize = 32;
        detailMetaText = CreateText("Post Meta", panel, string.Empty, 19, TextAnchor.MiddleLeft, new Vector2(194f, -116f), new Vector2(610f, -86f), new Color(.25f, .25f, .25f, 1f));

        detailPostContent = CreateDetailPostScroll(panel, new Vector2(36f, -830f), new Vector2(780f, -140f));
        Image mediaFrame = CreateImage("Post Media Frame", detailPostContent, Color.black, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -420f), new Vector2(744f, 0f)); mediaFrame.gameObject.AddComponent<RectMask2D>(); AddOutline(mediaFrame.gameObject, Color.black, new Vector2(2f, -2f));
        detailMediaFrame = mediaFrame.rectTransform;
        detailMedia = CreateFittedRawImage("Post Media", detailMediaFrame);
        detailMediaStatus = CreateStretchText("Media Status", detailMediaFrame, "", 19, TextAnchor.MiddleCenter, new Vector2(8f, 8f), new Vector2(-8f, -8f), Color.white);
        detailLikeButton = CreateButton("Like", detailPostContent, "LIKE 0", 19, new Vector2(0f, -488f), new Vector2(232f, -436f));
        detailFavoriteButton = CreateButton("Favorite", detailPostContent, "SAVE", 19, new Vector2(256f, -488f), new Vector2(488f, -436f));
        detailShareButton = CreateButton("Share", detailPostContent, "SHARE", 19, new Vector2(512f, -488f), new Vector2(744f, -436f));
        detailPlayLevelButton = CreateButton("Play Attached Level", detailPostContent, "PLAY ATTACHED LEVEL", 21, new Vector2(0f, -548f), new Vector2(744f, -496f));
        Image body = CreateImage("Body Background", detailPostContent, new Color(.86f, .86f, .84f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -800f), new Vector2(744f, -564f)); AddOutline(body.gameObject, Color.black, new Vector2(1.5f, -1.5f));
        detailBodyFrame = body.rectTransform;
        detailBodyText = CreateStretchText("Body", detailBodyFrame, string.Empty, 25, TextAnchor.UpperLeft, new Vector2(18f, 18f), new Vector2(-18f, -18f), Color.black);
        detailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailBodyText.verticalOverflow = VerticalWrapMode.Overflow;

        detailCommentsHeading = CreateText("Comments Heading", panel, "COMMENTS", 24, TextAnchor.MiddleLeft, new Vector2(810f, -174f), new Vector2(1484f, -140f), Color.black).rectTransform;
        Image comments = CreateImage("Comments Background", panel, new Color(.86f, .86f, .84f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(810f, -736f), new Vector2(1484f, -188f)); AddOutline(comments.gameObject, Color.black, new Vector2(1.5f, -1.5f));
        detailCommentsFrame = comments.rectTransform;
        commentContent = CreateVerticalScroll("Comments", detailCommentsFrame, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        commentInput = CreateInput("Comment", panel, "WRITE A COMMENT...", new Vector2(810f, -806f), new Vector2(1248f, -750f), false);
        detailCommentInputFrame = commentInput.GetComponent<RectTransform>();
        detailCommentButtonFrame = CreateButton("Post Comment", panel, "COMMENT", 18, new Vector2(1264f, -806f), new Vector2(1484f, -750f)).GetComponent<RectTransform>();
        commentStatusText = CreateText("Comment Status", panel, "", 17, TextAnchor.MiddleLeft, new Vector2(812f, -840f), new Vector2(1484f, -814f), new Color(.25f, .25f, .25f, 1f));
        detailCommentStatusFrame = commentStatusText.rectTransform;
    }

    private void BuildComposer(RectTransform panel)
    {
        CreateText("Title", panel, "CREATE POST", 46, TextAnchor.MiddleLeft, new Vector2(34f, -82f), new Vector2(600f, -24f), Color.black);
        composerAccountText = CreateText("Signed In", panel, "NOT SIGNED IN", 19, TextAnchor.MiddleRight, new Vector2(750f, -76f), new Vector2(1260f, -28f), new Color(.25f, .25f, .25f, 1f));
        CreateButton("Cancel", panel, "CANCEL", 21, new Vector2(1280f, -76f), new Vector2(1440f, -28f));
        composerTitleInput = CreateInput("Title", panel, "TITLE", new Vector2(38f, -184f), new Vector2(1440f, -128f), false);
        CreateButton("Choose Image", panel, "CHOOSE IMAGE (OPTIONAL)", 19, new Vector2(38f, -254f), new Vector2(332f, -198f));
        selectedImageText = CreateText("Selected Image", panel, "NO IMAGE SELECTED - TEXT POSTS ARE ALLOWED", 19, TextAnchor.MiddleLeft, new Vector2(352f, -252f), new Vector2(1440f, -200f), new Color(.25f, .25f, .25f, 1f));
        CreateButton("Attach Level", panel, "ATTACH LEVEL (OPTIONAL)", 19, new Vector2(38f, -324f), new Vector2(358f, -268f));
        selectedLevelText = CreateText("Selected Level", panel, "NO LEVEL ATTACHED", 19, TextAnchor.MiddleLeft, new Vector2(378f, -322f), new Vector2(1440f, -270f), new Color(.25f, .25f, .25f, 1f));
        composerBodyInput = CreateInput("Body", panel, "WRITE YOUR POST...", new Vector2(38f, -744f), new Vector2(1440f, -348f), true);
        CreateButton("Publish Post", panel, "PUBLISH POST", 23, new Vector2(1160f, -814f), new Vector2(1440f, -758f));
    }

    private void BuildLogin(RectTransform panel)
    {
        CreateText("Title", panel, "ACCOUNT", 46, TextAnchor.MiddleLeft, new Vector2(34f, -82f), new Vector2(600f, -24f), Color.black);
        CreateText("Hint", panel, "Register once, then use the same account on any device.", 20, TextAnchor.MiddleLeft, new Vector2(38f, -124f), new Vector2(1320f, -90f), new Color(.25f, .25f, .25f, 1f));
        loginUsernameInput = CreateInput("Username", panel, "USERNAME", new Vector2(390f, -280f), new Vector2(1130f, -218f), false);
        loginPasswordInput = CreateInput("Password", panel, "PASSWORD", new Vector2(390f, -360f), new Vector2(1130f, -298f), false); loginPasswordInput.contentType = InputField.ContentType.Password;
        CreateButton("Register", panel, "REGISTER", 22, new Vector2(390f, -450f), new Vector2(735f, -388f));
        CreateButton("Login", panel, "LOGIN", 22, new Vector2(755f, -450f), new Vector2(1130f, -388f));
        CreateButton("Cancel", panel, "CANCEL", 21, new Vector2(1280f, -76f), new Vector2(1440f, -28f));
        loginStatusText = CreateText("Status", panel, "", 21, TextAnchor.MiddleCenter, new Vector2(250f, -530f), new Vector2(1270f, -482f), Color.black);
    }

    private void BuildMyPosts(RectTransform panel)
    {
        CreateText("Title", panel, "MY POSTS", 46, TextAnchor.MiddleLeft, new Vector2(34f, -82f), new Vector2(600f, -24f), Color.black);
        CreateText("Hint", panel, "Manage your own posts here.", 20, TextAnchor.MiddleLeft, new Vector2(38f, -120f), new Vector2(1120f, -88f), new Color(.25f, .25f, .25f, 1f));
        CreateButton("Back", panel, "BACK", 21, new Vector2(1180f, -76f), new Vector2(1320f, -26f));
        CreateButton("Close", panel, "X", 30, new Vector2(1392f, -76f), new Vector2(1440f, -26f));
        Image background = CreateImage("Posts Background", panel, new Color(.84f, .84f, .82f, 1f), Vector2.zero, Vector2.one, new Vector2(30f, 28f), new Vector2(-30f, -146f)); AddOutline(background.gameObject, Color.black, new Vector2(1.5f, -1.5f));
        myPostsContent = CreateGridScroll("My Posts", background.GetComponent<RectTransform>(), new Vector2(10f, 10f), new Vector2(-10f, -10f));
    }

    private void BuildFavorites(RectTransform panel)
    {
        CreateText("Title", panel, "SAVED POSTS", 46, TextAnchor.MiddleLeft, new Vector2(34f, -82f), new Vector2(600f, -24f), Color.black);
        CreateText("Hint", panel, "Your saved posts are private to this account.", 20, TextAnchor.MiddleLeft, new Vector2(38f, -120f), new Vector2(1120f, -88f), new Color(.25f, .25f, .25f, 1f));
        CreateButton("Back", panel, "BACK", 21, new Vector2(1180f, -76f), new Vector2(1320f, -26f));
        CreateButton("Close", panel, "X", 30, new Vector2(1392f, -76f), new Vector2(1440f, -26f));
        Image background = CreateImage("Posts Background", panel, new Color(.84f, .84f, .82f, 1f), Vector2.zero, Vector2.one, new Vector2(30f, 28f), new Vector2(-30f, -146f)); AddOutline(background.gameObject, Color.black, new Vector2(1.5f, -1.5f));
        favoritesContent = CreateGridScroll("Favorites", background.GetComponent<RectTransform>(), new Vector2(10f, 10f), new Vector2(-10f, -10f));
    }

    private void RefreshFeed()
    {
        if (isBusy) return;
        ClearChildren(feedContent); SetBusy(true, "Loading posts...");
        StartCoroutine(CommunityLevelApi.GetPosts(apiBaseUrl, ShowPosts, error => SetBusy(false, "Could not load posts: " + error)));
    }

    private void ShowPosts(CommunityPostListResponse response)
    {
        CommunityPostSummary[] posts = response.posts ?? Array.Empty<CommunityPostSummary>(); SetBusy(false, posts.Length == 0 ? "No posts yet." : posts.Length + " posts");
        foreach (CommunityPostSummary post in posts) if (post != null && !string.IsNullOrWhiteSpace(post.id)) CreateFeedCard(feedContent, post, false);
    }

    private void CreateFeedCard(RectTransform parent, CommunityPostSummary post, bool includeManagement)
    {
        Button card = CreateButton("Post - " + post.id, parent, string.Empty, 20, Vector2.zero, Vector2.zero); card.GetComponent<Image>().color = Color.white;
        RectTransform rect = card.GetComponent<RectTransform>();
        bool hasImage = !string.IsNullOrWhiteSpace(post.mediaUrl);
        if (hasImage)
        {
            Image frame = CreateImage("Media Frame", rect, Color.black, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -386.625f), new Vector2(678f, -12f)); frame.gameObject.AddComponent<RectMask2D>();
            RawImage media = CreateFittedRawImage("Media", frame.GetComponent<RectTransform>());
            media.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            Text label = CreateStretchText("Media Status", frame.GetComponent<RectTransform>(), "LOADING IMAGE...", 18, TextAnchor.MiddleCenter, new Vector2(8f, 8f), new Vector2(-8f, -8f), Color.white); label.raycastTarget = false;
            StartCoroutine(LoadMonochromeImage(post.mediaUrl, media, label));
        }
        string title = string.IsNullOrWhiteSpace(post.title) ? "Untitled Post" : post.title;
        string preview = string.IsNullOrWhiteSpace(post.bodyPreview) ? string.Empty : "\n" + post.bodyPreview;
        Vector2 textMin = includeManagement && hasImage ? new Vector2(18f, -480f) : hasImage ? new Vector2(18f, -492f) : new Vector2(18f, -340f);
        Vector2 textMax = hasImage ? new Vector2(674f, -400f) : new Vector2(674f, -20f);
        Text body = CreateText("Post Text", rect, title + "\nBY " + post.author + "  /  " + post.likeCount + " LIKES  /  " + post.commentCount + " COMMENTS" + (post.hasLevel ? "  /  PLAYABLE LEVEL" : string.Empty) + preview, 19, TextAnchor.UpperLeft, textMin, textMax, Color.black); body.resizeTextForBestFit = true; body.resizeTextMinSize = 13; body.resizeTextMaxSize = 19; body.raycastTarget = false;
        card.onClick.AddListener(() => OpenPost(post.id));
        if (includeManagement)
        {
            Button edit = CreateButton("Edit", rect, "EDIT", 16, new Vector2(400f, -538f), new Vector2(532f, -494f));
            edit.onClick.AddListener(() => EditPost(post.id));
            Button delete = CreateButton("Delete", rect, "DELETE", 16, new Vector2(542f, -538f), new Vector2(674f, -494f));
            delete.onClick.AddListener(() => DeletePost(post.id, OpenMyPosts));
        }
    }

    private void OpenPost(string postId)
    {
        if (isBusy) return;
        SetBusy(true, "Opening post..."); StartCoroutine(CommunityLevelApi.GetPost(apiBaseUrl, postId, ShowPostDetail, error => SetBusy(false, "Could not open post: " + error)));
    }

    private void ShowPostDetail(CommunityPostDetail post)
    {
        openedPost = post; detailTitleText.text = string.IsNullOrWhiteSpace(post.title) ? "Untitled Post" : post.title;
        detailMetaText.text = GetTypeLabel(post.type) + "  /  BY " + post.author + "  /  " + post.likeCount + " LIKES  /  " + (post.comments?.Length ?? 0) + " COMMENTS" + (post.hasLevel ? "  /  PLAYABLE LEVEL" : string.Empty);
        detailBodyText.text = string.IsNullOrWhiteSpace(post.body) ? "No text." : post.body;
        bool hasMedia = !string.IsNullOrWhiteSpace(post.mediaUrl); detailMediaAspectRatio = 16f / 9f; detailMediaFrame.gameObject.SetActive(hasMedia); detailMedia.gameObject.SetActive(hasMedia); detailMediaStatus.gameObject.SetActive(hasMedia); detailMedia.texture = null; detailMedia.color = new Color(.18f, .18f, .18f, 1f); detailMediaStatus.text = hasMedia ? "LOADING IMAGE..." : string.Empty;
        if (hasMedia) StartCoroutine(LoadMonochromeImage(post.mediaUrl, detailMedia, detailMediaStatus));
        detailPlayLevelButton.gameObject.SetActive(post.hasLevel && !string.IsNullOrWhiteSpace(post.level));
        detailLikeButton.gameObject.SetActive(true);
        detailFavoriteButton.gameObject.SetActive(true);
        detailShareButton.gameObject.SetActive(true);
        LayoutDetailForMedia(hasMedia ? detailMediaAspectRatio : 1f);
        detailPostContent.anchoredPosition = Vector2.zero;
        if (detailLayoutRoutine != null) StopCoroutine(detailLayoutRoutine);
        detailLayoutRoutine = StartCoroutine(RebuildDetailLayoutAfterImageLoad());
        UpdateDetailLikeUi();
        UpdateDetailFavoriteUi();
        ShowComments(post.comments); SetBusy(false, string.Empty); ShowOnly(detailRoot);
    }

    private void ShowComments(CommunityComment[] comments)
    {
        ClearChildren(commentContent);
        if (comments == null || comments.Length == 0) { CreateComment("No comments yet.", string.Empty); return; }
        foreach (CommunityComment comment in comments) CreateComment(comment.author, comment.body);
    }

    private void CreateComment(string author, string body)
    {
        GameObject item = new("Comment", typeof(RectTransform), typeof(Image), typeof(LayoutElement)); item.transform.SetParent(commentContent, false); item.GetComponent<Image>().color = Color.white; AddOutline(item, Color.black, new Vector2(1f, -1f)); item.GetComponent<LayoutElement>().preferredHeight = 70f;
        Text text = CreateStretchText("Text", item.GetComponent<RectTransform>(), string.IsNullOrEmpty(body) ? author : author + "\n" + body, 17, TextAnchor.UpperLeft, new Vector2(10f, 6f), new Vector2(-10f, -6f), Color.black); text.raycastTarget = false;
    }

    private void PublishComment()
    {
        if (openedPost == null || isBusy) return;
        if (!CommunityAccount.IsSignedIn) { commentStatusText.text = "SIGN IN TO COMMENT."; return; }
        SetBusy(true, "Publishing comment...");
        StartCoroutine(CommunityLevelApi.PublishComment(apiBaseUrl, openedPost.id, new CommunityCommentRequest { body = commentInput.text }, _ => { commentInput.SetTextWithoutNotify(string.Empty); commentStatusText.text = "COMMENT POSTED."; SetBusy(false, string.Empty); OpenPost(openedPost.id); }, error => { SetBusy(false, string.Empty); commentStatusText.text = "COMMENT FAILED: " + error; }));
    }

    private void DeletePost(string postId, UnityAction success)
    {
        if (isBusy) return; SetBusy(true, "Deleting post...");
        StartCoroutine(CommunityLevelApi.DeletePost(apiBaseUrl, postId, _ => { SetBusy(false, "Post deleted."); success?.Invoke(); RefreshFeed(); }, error => SetBusy(false, "Delete failed: " + error)));
    }

    private void EditPost(string postId)
    {
        if (isBusy) return;
        SetBusy(true, "Opening post editor...");
        StartCoroutine(CommunityLevelApi.GetPost(apiBaseUrl, postId, post => { openedPost = post; SetBusy(false, string.Empty); OpenComposerForEdit(); }, error => SetBusy(false, "Could not edit post: " + error)));
    }

    private void OpenComposer()
    {
        if (!CommunityAccount.IsSignedIn) { OpenLogin(); return; }
        editingPostId = string.Empty;
        editingMediaUrl = string.Empty;
        editingLevelJson = string.Empty;
        selectedLevelJson = string.Empty;
        UpdateComposerAccountUi();
        composerTitleInput.SetTextWithoutNotify(string.Empty); composerBodyInput.SetTextWithoutNotify(string.Empty); selectedImagePath = string.Empty; selectedImageText.text = "NO IMAGE SELECTED - TEXT POSTS ARE ALLOWED"; selectedLevelText.text = "NO LEVEL ATTACHED"; SetButtonLabel("Post Composer/Publish Post", "PUBLISH POST"); ShowOnly(composerRoot);
    }

    private void OpenComposerWithDraft(CommunityPostDraft draft)
    {
        if (draft == null) { OpenComposer(); return; }
        if (!CommunityAccount.IsSignedIn) { pendingDraft = draft; OpenLogin(); return; }
        pendingDraft = null;
        editingPostId = string.Empty;
        editingMediaUrl = string.Empty;
        editingLevelJson = string.Empty;
        selectedImagePath = string.Empty;
        selectedLevelJson = draft.level ?? string.Empty;
        UpdateComposerAccountUi();
        composerTitleInput.SetTextWithoutNotify(draft.title ?? string.Empty);
        composerBodyInput.SetTextWithoutNotify(draft.body ?? string.Empty);
        selectedImageText.text = "NO IMAGE SELECTED - TEXT POSTS ARE ALLOWED";
        selectedLevelText.text = string.IsNullOrWhiteSpace(selectedLevelJson) ? "NO LEVEL ATTACHED" : "CURRENT LEVEL ATTACHED";
        SetButtonLabel("Post Composer/Publish Post", "PUBLISH POST");
        ShowOnly(composerRoot);
    }

    private void OpenComposerForEdit()
    {
        if (openedPost == null || !CommunityAccount.IsSignedIn || !string.Equals(openedPost.author, CommunityAccount.Username, StringComparison.Ordinal)) return;
        editingPostId = openedPost.id;
        editingMediaUrl = openedPost.mediaUrl ?? string.Empty;
        editingLevelJson = openedPost.level ?? string.Empty;
        selectedLevelJson = editingLevelJson;
        composerTitleInput.SetTextWithoutNotify(openedPost.title);
        composerBodyInput.SetTextWithoutNotify(openedPost.body);
        selectedImagePath = string.Empty;
        UpdateComposerAccountUi();
        selectedImageText.text = string.IsNullOrWhiteSpace(editingMediaUrl) ? "NO IMAGE SELECTED - TEXT POSTS ARE ALLOWED" : "CURRENT IMAGE WILL BE KEPT";
        selectedLevelText.text = string.IsNullOrWhiteSpace(editingLevelJson) ? "NO LEVEL ATTACHED" : "CURRENT LEVEL WILL BE KEPT";
        SetButtonLabel("Post Composer/Publish Post", "SAVE EDIT");
        ShowOnly(composerRoot);
    }

    private void PublishComposerPost()
    {
        if (isBusy || !CommunityAccount.IsSignedIn) { if (!CommunityAccount.IsSignedIn) OpenLogin(); return; }
        if (string.IsNullOrWhiteSpace(selectedImagePath)) { PublishPostWithImage(string.IsNullOrWhiteSpace(editingPostId) ? string.Empty : editingMediaUrl); return; }
        byte[] imageBytes; try { imageBytes = File.ReadAllBytes(selectedImagePath); } catch (Exception exception) { SetBusy(false, "Could not read image: " + exception.Message); return; }
        SetBusy(true, "Uploading image..."); StartCoroutine(CommunityLevelApi.UploadImage(apiBaseUrl, imageBytes, selectedImagePath, PublishPostWithImage, error => SetBusy(false, "Image upload failed: " + error)));
    }

    private void PublishPostWithImage(string mediaUrl)
    {
        SetBusy(true, "Publishing post...");
        string levelJson = string.IsNullOrWhiteSpace(selectedLevelJson) ? editingLevelJson : selectedLevelJson;
        CommunityPostPublishRequest request = new() { type = string.IsNullOrWhiteSpace(levelJson) ? "image" : "level", title = composerTitleInput.text, author = CommunityAccount.Username, body = composerBodyInput.text, mediaUrl = mediaUrl, level = levelJson };
        if (string.IsNullOrWhiteSpace(editingPostId))
        {
            StartCoroutine(CommunityLevelApi.PublishPost(apiBaseUrl, request, _ => { SetBusy(false, "Post published."); ShowFeed(); RefreshFeed(); }, error => SetBusy(false, "Publish failed: " + error)));
            return;
        }

        string postId = editingPostId;
        StartCoroutine(CommunityLevelApi.UpdatePost(apiBaseUrl, postId, request, _ => { editingPostId = string.Empty; editingMediaUrl = string.Empty; editingLevelJson = string.Empty; selectedLevelJson = string.Empty; SetBusy(false, "Post updated."); ShowFeed(); RefreshFeed(); }, error => SetBusy(false, "Edit failed: " + error)));
    }

    private void ToggleOpenedPostLike()
    {
        if (openedPost == null || isBusy) return;
        if (!CommunityAccount.IsSignedIn) { OpenLogin(); return; }
        SetBusy(true, "Updating like...");
        StartCoroutine(CommunityLevelApi.ToggleLike(apiBaseUrl, openedPost.id, response => { openedPost.likedByCurrentUser = response.liked; openedPost.likeCount = response.likeCount; UpdateDetailLikeUi(); SetBusy(false, string.Empty); }, error => SetBusy(false, "Like failed: " + error)));
    }

    private void UpdateDetailLikeUi()
    {
        if (openedPost == null || detailLikeButton == null) return;
        SetButtonLabel(detailLikeButton, (openedPost.likedByCurrentUser ? "UNLIKE " : "LIKE ") + openedPost.likeCount);
    }

    private void ToggleOpenedPostFavorite()
    {
        if (openedPost == null || isBusy) return;
        if (!CommunityAccount.IsSignedIn) { OpenLogin(); return; }
        SetBusy(true, "Updating saved post...");
        StartCoroutine(CommunityLevelApi.ToggleFavorite(apiBaseUrl, openedPost.id, response => { openedPost.favoritedByCurrentUser = response.favorited; UpdateDetailFavoriteUi(); SetBusy(false, string.Empty); }, error => SetBusy(false, "Save failed: " + error)));
    }

    private void UpdateDetailFavoriteUi()
    {
        if (openedPost == null || detailFavoriteButton == null) return;
        SetButtonLabel(detailFavoriteButton, openedPost.favoritedByCurrentUser ? "REMOVE SAVE" : "SAVE");
    }

    private void ShareOpenedPost()
    {
        if (openedPost == null) return;
        GUIUtility.systemCopyBuffer = apiBaseUrl.TrimEnd('/') + "/api/posts/" + openedPost.id;
        commentStatusText.text = "POST LINK COPIED.";
    }

    private void OpenLogin()
    {
        loginUsernameInput.SetTextWithoutNotify(CommunityAccount.Username); loginPasswordInput.SetTextWithoutNotify(string.Empty); loginStatusText.text = CommunityAccount.IsSignedIn ? "SIGNED IN AS " + CommunityAccount.Username : string.Empty; ShowOnly(loginRoot);
    }
    private void Register() => Authenticate(true);
    private void Login() => Authenticate(false);
    private void Authenticate(bool register)
    {
        if (isBusy) return; SetBusy(true, register ? "Creating account..." : "Signing in...");
        CommunityCredentials credentials = new() { username = loginUsernameInput.text, password = loginPasswordInput.text };
        StartCoroutine(register ? CommunityLevelApi.Register(apiBaseUrl, credentials, HandleAuth, HandleAuthError) : CommunityLevelApi.Login(apiBaseUrl, credentials, HandleAuth, HandleAuthError));
    }
    private void HandleAuth(CommunityAuthResponse response) { CommunityAccount.Set(response); SetBusy(false, string.Empty); UpdateAccountUi(); loginStatusText.text = "SIGNED IN AS " + CommunityAccount.Username; if (pendingDraft != null) OpenComposerWithDraft(pendingDraft); else ShowFeed(); RefreshFeed(); }
    private void HandleAuthError(string error) { SetBusy(false, string.Empty); loginStatusText.text = error; }
    private void UpdateAccountUi() { if (accountText != null) accountText.text = CommunityAccount.IsSignedIn ? "SIGNED IN AS " + CommunityAccount.Username : "NOT SIGNED IN"; }
    private void UpdateComposerAccountUi() { if (composerAccountText != null) composerAccountText.text = CommunityAccount.IsSignedIn ? "SIGNED IN AS " + CommunityAccount.Username : "NOT SIGNED IN"; }

    private void OpenMyPosts()
    {
        if (!CommunityAccount.IsSignedIn) { OpenLogin(); return; }
        ClearChildren(myPostsContent); ShowOnly(myPostsRoot); SetBusy(true, "Loading your posts...");
        StartCoroutine(CommunityLevelApi.GetMyPosts(apiBaseUrl, response => { SetBusy(false, response.posts.Length == 0 ? "No posts yet." : string.Empty); foreach (CommunityPostSummary post in response.posts) CreateFeedCard(myPostsContent, post, true); }, error => SetBusy(false, "Could not load your posts: " + error)));
    }

    private void OpenFavorites()
    {
        if (!CommunityAccount.IsSignedIn) { OpenLogin(); return; }
        ClearChildren(favoritesContent); ShowOnly(favoritesRoot); SetBusy(true, "Loading saved posts...");
        StartCoroutine(CommunityLevelApi.GetFavorites(apiBaseUrl, response => { SetBusy(false, response.posts.Length == 0 ? "No saved posts yet." : string.Empty); foreach (CommunityPostSummary post in response.posts) CreateFeedCard(favoritesContent, post, false); }, error => SetBusy(false, "Could not load saved posts: " + error)));
    }

    private void PlayOpenedLevel()
    {
        if (openedPost == null || string.IsNullOrWhiteSpace(openedPost.level)) return;
        if (!LevelJsonUtility.TryParse(openedPost.level, out _, out string error)) { SetBusy(false, "Attached level is invalid: " + error); return; }
        LevelSceneModeRequest.Clear(); LevelLoadRequest.Set(openedPost.level, "Community: " + openedPost.title, 0); CardSelectionState.PrepareLevelLoad(PlaySceneName); SceneTransitionController.LoadScene(PlaySceneName);
    }

    private void ShowFeed() { ShowOnly(feedRoot); }
    private void ShowOnly(GameObject active) { feedRoot.SetActive(active == feedRoot); detailRoot.SetActive(active == detailRoot); composerRoot.SetActive(active == composerRoot); loginRoot.SetActive(active == loginRoot); myPostsRoot.SetActive(active == myPostsRoot); favoritesRoot.SetActive(active == favoritesRoot); }
    private void SetBusy(bool value, string status) { isBusy = value; if (statusText != null) statusText.text = status; }
    private void Close() { SceneTransitionController.LoadScene(LevelSelectSceneName); }
    private static void ClearChildren(RectTransform parent) { if (parent == null) return; for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject); }
    private void SetButtonLabel(string path, string value) { Transform target = transform.Find(path); if (target != null && target.Find("Label") != null && target.Find("Label").TryGetComponent(out Text text)) text.text = value; }
    private static void SetButtonLabel(Button button, string value) { if (button != null && button.transform.Find("Label") != null && button.transform.Find("Label").TryGetComponent(out Text text)) text.text = value; }

    private IEnumerator LoadMonochromeImage(string url, RawImage target, Text label)
    {
        if (string.IsNullOrWhiteSpace(url)) { SetImageUnavailable(label); yield break; }
        if (monochromeImageCache.TryGetValue(url, out Texture2D cached)) { ApplyMonochromeImage(cached, target, label); yield break; }

        if (imageLoadsInProgress.Contains(url))
        {
            while (imageLoadsInProgress.Contains(url)) yield return null;
            if (monochromeImageCache.TryGetValue(url, out cached)) ApplyMonochromeImage(cached, target, label);
            else SetImageUnavailable(label);
            yield break;
        }

        imageLoadsInProgress.Add(url);
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, false); yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success) { imageLoadsInProgress.Remove(url); SetImageUnavailable(label); yield break; }
        Texture2D source = DownloadHandlerTexture.GetContent(request); if (source == null) { imageLoadsInProgress.Remove(url); SetImageUnavailable(label); yield break; }
        Texture2D monochrome = new(source.width, source.height, TextureFormat.RGBA32, false); Color32[] pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++) { byte shade = (byte)((pixels[i].r * 30 + pixels[i].g * 59 + pixels[i].b * 11) / 100); pixels[i] = new Color32(shade, shade, shade, pixels[i].a); }
        monochrome.SetPixels32(pixels); monochrome.Apply(false, true); Destroy(source);
        monochromeImageCache[url] = monochrome;
        imageLoadsInProgress.Remove(url);
        ApplyMonochromeImage(monochrome, target, label);
    }

    private void ApplyMonochromeImage(Texture2D texture, RawImage target, Text label)
    {
        if (target == null || texture == null) { SetImageUnavailable(label); return; }
        target.texture = texture; target.color = Color.white; if (label != null) label.gameObject.SetActive(false);
        if (target.TryGetComponent(out AspectRatioFitter fitter))
        {
            fitter.aspectRatio = (float)texture.width / texture.height;
        }
        if (target == detailMedia)
        {
            detailMediaAspectRatio = (float)texture.width / texture.height;
            LayoutDetailForMedia(detailMediaAspectRatio);
        }
    }

    private IEnumerator RebuildDetailLayoutAfterImageLoad()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (detailRoot != null && detailRoot.activeInHierarchy && detailMedia != null && detailMedia.texture != null)
        {
            detailMediaAspectRatio = (float)detailMedia.texture.width / detailMedia.texture.height;
            LayoutDetailForMedia(detailMediaAspectRatio);
        }
        detailLayoutRoutine = null;
    }

    private void LayoutDetailForMedia(float aspectRatio)
    {
        if (detailPostContent == null || detailMediaFrame == null || detailBodyFrame == null || detailBodyText == null) return;

        const float contentWidth = 744f;
        float safeAspect = Mathf.Max(.1f, aspectRatio);
        bool hasMedia = detailMediaFrame.gameObject.activeSelf;
        float mediaHeight = hasMedia ? contentWidth / safeAspect : 0f;
        if (hasMedia) SetTopLeftRect(detailMediaFrame, new Vector2(0f, -mediaHeight), new Vector2(contentWidth, 0f));

        float actionTop = -mediaHeight - (hasMedia ? 16f : 0f);
        float actionBottom = actionTop - 52f;
        const float actionGap = 24f;
        float actionWidth = (contentWidth - actionGap * 2f) / 3f;
        SetTopLeftRect(detailLikeButton.GetComponent<RectTransform>(), new Vector2(0f, actionBottom), new Vector2(actionWidth, actionTop));
        SetTopLeftRect(detailFavoriteButton.GetComponent<RectTransform>(), new Vector2(actionWidth + actionGap, actionBottom), new Vector2(actionWidth * 2f + actionGap, actionTop));
        SetTopLeftRect(detailShareButton.GetComponent<RectTransform>(), new Vector2(actionWidth * 2f + actionGap * 2f, actionBottom), new Vector2(contentWidth, actionTop));

        float contentTop = actionBottom - 8f;
        bool hasAttachedLevel = detailPlayLevelButton.gameObject.activeSelf;
        if (hasAttachedLevel)
        {
            SetTopLeftRect(detailPlayLevelButton.GetComponent<RectTransform>(), new Vector2(0f, contentTop - 52f), new Vector2(contentWidth, contentTop));
            contentTop -= 60f;
        }

        // Measure at the final column width, then resize the article so it belongs to the same left scroll content as the image.
        SetTopLeftRect(detailBodyFrame, new Vector2(0f, contentTop - 180f), new Vector2(contentWidth, contentTop));
        Canvas.ForceUpdateCanvases();
        float bodyHeight = Mathf.Max(180f, detailBodyText.preferredHeight + 36f);
        float bodyBottom = contentTop - bodyHeight;
        SetTopLeftRect(detailBodyFrame, new Vector2(0f, bodyBottom), new Vector2(contentWidth, contentTop));
        detailPostContent.sizeDelta = new Vector2(0f, -bodyBottom + 18f);
        ScrollRect postScroll = detailPostContent.parent.GetComponent<ScrollRect>();
        if (postScroll != null && postScroll.verticalScrollbar != null)
        {
            postScroll.verticalScrollbar.size = Mathf.Clamp01(postScroll.viewport.rect.height / detailPostContent.rect.height);
        }
    }

    private static void SetImageUnavailable(Text label) { if (label != null) label.text = "IMAGE UNAVAILABLE"; }

    private static string GetTypeLabel(string type) => type == "level" ? "PLAYABLE LEVEL" : "POST";
    private void ChooseImage() { string path = OpenImageFilePicker(); if (string.IsNullOrWhiteSpace(path)) return; selectedImagePath = path; selectedImageText.text = Path.GetFileName(path); }
    private void ChooseLevel()
    {
        string path = OpenLevelFilePicker();
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            string json = File.ReadAllText(path);
            if (!LevelJsonUtility.TryParse(json, out _, out string error)) { SetBusy(false, "Invalid level: " + error); return; }
            selectedLevelJson = json;
            selectedLevelText.text = Path.GetFileName(path);
        }
        catch (Exception exception)
        {
            SetBusy(false, "Could not read level: " + exception.Message);
        }
    }

#if UNITY_STANDALONE_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct OpenFileName { public int lStructSize; public IntPtr hwndOwner; public IntPtr hInstance; public IntPtr lpstrFilter; public IntPtr lpstrCustomFilter; public int nMaxCustFilter; public int nFilterIndex; public IntPtr lpstrFile; public int nMaxFile; public IntPtr lpstrFileTitle; public int nMaxFileTitle; public IntPtr lpstrInitialDir; public IntPtr lpstrTitle; public int Flags; public short nFileOffset; public short nFileExtension; public IntPtr lpstrDefExt; public IntPtr lCustData; public IntPtr lpfnHook; public IntPtr lpTemplateName; public IntPtr pvReserved; public int dwReserved; public int FlagsEx; }
    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetOpenFileName(ref OpenFileName ofn);
#endif
    private static string OpenImageFilePicker()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Choose Community Image", string.Empty, "png,jpg,jpeg,webp");
#elif UNITY_STANDALONE_WIN
        return OpenWindowsFilePicker("Choose Community Image", "Image Files\0*.png;*.jpg;*.jpeg;*.webp\0\0");
#else
        return string.Empty;
#endif
    }

    private static string OpenLevelFilePicker()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Attach Community Level", Application.persistentDataPath, "json");
#elif UNITY_STANDALONE_WIN
        return OpenWindowsFilePicker("Attach Community Level", "Level JSON\0*.json\0\0");
#else
        return string.Empty;
#endif
    }

#if UNITY_STANDALONE_WIN
    private static string OpenWindowsFilePicker(string titleText, string filterText)
    {
        const int length = 4096; IntPtr buffer = Marshal.AllocHGlobal(length * sizeof(char)); IntPtr filter = Marshal.StringToHGlobalUni(filterText); IntPtr title = Marshal.StringToHGlobalUni(titleText);
        for (int i = 0; i < length; i++) Marshal.WriteInt16(buffer, i * sizeof(char), 0);
        try { OpenFileName dialog = new() { lStructSize = Marshal.SizeOf<OpenFileName>(), lpstrFilter = filter, lpstrFile = buffer, nMaxFile = length, lpstrTitle = title, Flags = 0x00000008 | 0x00000800 }; return GetOpenFileName(ref dialog) ? Marshal.PtrToStringUni(buffer) : string.Empty; }
        finally { Marshal.FreeHGlobal(title); Marshal.FreeHGlobal(filter); Marshal.FreeHGlobal(buffer); }
    }
#endif

    private RectTransform CreateGridScroll(string name, RectTransform parent, Vector2 min, Vector2 max)
    {
        GameObject viewportObject = new(name + " Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)); viewportObject.transform.SetParent(parent, false); viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, .01f); RectTransform viewport = viewportObject.GetComponent<RectTransform>(); Stretch(viewport, Vector2.zero, Vector2.one, min, max);
        GameObject contentObject = new(name + " Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter)); contentObject.transform.SetParent(viewport, false); RectTransform content = contentObject.GetComponent<RectTransform>(); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(.5f, 1f); content.sizeDelta = Vector2.zero;
        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>(); grid.padding = new RectOffset(14, 14, 14, 14); grid.cellSize = new Vector2(690f, 560f); grid.spacing = new Vector2(14f, 14f); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2; grid.childAlignment = TextAnchor.UpperCenter;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; ScrollRect scroll = parent.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36f; return content;
    }
    private RectTransform CreateVerticalScroll(string name, RectTransform parent, Vector2 min, Vector2 max)
    {
        GameObject viewportObject = new(name + " Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)); viewportObject.transform.SetParent(parent, false); viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, .01f); RectTransform viewport = viewportObject.GetComponent<RectTransform>(); Stretch(viewport, Vector2.zero, Vector2.one, min, max);
        GameObject contentObject = new(name + " Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); contentObject.transform.SetParent(viewport, false); RectTransform content = contentObject.GetComponent<RectTransform>(); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(.5f, 1f); content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(8, 8, 8, 8); layout.spacing = 8f; layout.childControlWidth = true; layout.childControlHeight = false; contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; ScrollRect scroll = parent.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 28f; return content;
    }
    private RectTransform CreateDetailPostScroll(RectTransform parent, Vector2 min, Vector2 max)
    {
        GameObject viewportObject = new("Post Content Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)); viewportObject.transform.SetParent(parent, false); Image viewportImage = viewportObject.GetComponent<Image>(); viewportImage.color = new Color(1f, 1f, 1f, .01f); viewportImage.raycastTarget = false; RectTransform viewport = viewportObject.GetComponent<RectTransform>(); SetTopLeftRect(viewport, min, max);
        GameObject contentObject = new("Post Content", typeof(RectTransform)); contentObject.transform.SetParent(viewport, false); RectTransform content = contentObject.GetComponent<RectTransform>(); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(.5f, 1f); content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero; content.sizeDelta = new Vector2(0f, 720f);
        GameObject barObject = new("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)); barObject.transform.SetParent(viewport, false); Image barImage = barObject.GetComponent<Image>(); barImage.color = Color.black; RectTransform bar = barObject.GetComponent<RectTransform>(); bar.anchorMin = new Vector2(1f, 0f); bar.anchorMax = new Vector2(1f, 1f); bar.pivot = new Vector2(1f, .5f); bar.sizeDelta = new Vector2(12f, -12f); bar.anchoredPosition = new Vector2(-4f, 0f);
        GameObject handleObject = new("Handle", typeof(RectTransform), typeof(Image)); handleObject.transform.SetParent(bar, false); Image handleImage = handleObject.GetComponent<Image>(); handleImage.color = Color.white; AddOutline(handleObject, Color.black, new Vector2(1f, -1f)); RectTransform handle = handleObject.GetComponent<RectTransform>(); Stretch(handle, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        Scrollbar scrollbar = barObject.GetComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.direction = Scrollbar.Direction.BottomToTop;
        ScrollRect scroll = viewportObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36f; scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return content;
    }
    private RectTransform CreateTextScroll(string name, RectTransform parent, Vector2 min, Vector2 max, out Text output)
    {
        GameObject viewportObject = new(name + " Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)); viewportObject.transform.SetParent(parent, false); viewportObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, .01f); RectTransform viewport = viewportObject.GetComponent<RectTransform>(); Stretch(viewport, Vector2.zero, Vector2.one, min, max);
        GameObject contentObject = new(name + " Content", typeof(RectTransform), typeof(ContentSizeFitter)); contentObject.transform.SetParent(viewport, false); RectTransform content = contentObject.GetComponent<RectTransform>(); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(.5f, 1f); content.sizeDelta = Vector2.zero; output = CreateText("Body", content, string.Empty, 25, TextAnchor.UpperLeft, Vector2.zero, Vector2.zero, Color.black); output.rectTransform.anchorMin = new Vector2(0f, 1f); output.rectTransform.anchorMax = new Vector2(1f, 1f); output.rectTransform.pivot = new Vector2(.5f, 1f); output.rectTransform.sizeDelta = Vector2.zero;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; ScrollRect scroll = parent.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 32f; return content;
    }
    private Image CreateImage(string name, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) { GameObject item = new(name, typeof(RectTransform), typeof(Image)); item.transform.SetParent(parent, false); Image image = item.GetComponent<Image>(); image.color = color; Stretch(image.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax); return image; }
    private RawImage CreateRawImage(string name, RectTransform parent, Vector2 offsetMin, Vector2 offsetMax) { GameObject item = new(name, typeof(RectTransform), typeof(RawImage)); item.transform.SetParent(parent, false); RawImage image = item.GetComponent<RawImage>(); SetTopLeftRect(image.rectTransform, offsetMin, offsetMax); return image; }
    private RawImage CreateFittedRawImage(string name, RectTransform parent) { RawImage image = CreateRawImage(name, parent, Vector2.zero, Vector2.zero); Stretch(image.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); AspectRatioFitter fitter = image.gameObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; return image; }
    private Text CreateText(string name, RectTransform parent, string value, int size, TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax, Color color) { GameObject item = new(name, typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent, false); Text text = item.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; SetTopLeftRect(text.rectTransform, offsetMin, offsetMax); return text; }
    private Text CreateStretchText(string name, RectTransform parent, string value, int size, TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax, Color color) { Text text = CreateText(name, parent, value, size, alignment, Vector2.zero, Vector2.zero, color); Stretch(text.rectTransform, Vector2.zero, Vector2.one, offsetMin, offsetMax); return text; }
    private Button CreateButton(string name, RectTransform parent, string label, int size, Vector2 offsetMin, Vector2 offsetMax) { GameObject item = new(name, typeof(RectTransform), typeof(Image), typeof(Button)); item.transform.SetParent(parent, false); Image image = item.GetComponent<Image>(); image.color = new Color(.92f, .92f, .9f, 1f); AddOutline(item, Color.black, new Vector2(1.5f, -1.5f)); Button button = item.GetComponent<Button>(); ColorBlock colors = button.colors; colors.normalColor = new Color(.92f, .92f, .9f, 1f); colors.highlightedColor = new Color(.74f, .74f, .72f, 1f); colors.pressedColor = new Color(.5f, .5f, .48f, 1f); button.colors = colors; SetTopLeftRect(item.GetComponent<RectTransform>(), offsetMin, offsetMax); Text text = CreateStretchText("Label", item.GetComponent<RectTransform>(), label, size, TextAnchor.MiddleCenter, new Vector2(10f, 4f), new Vector2(-10f, -4f), Color.black); text.raycastTarget = false; return button; }
    private InputField CreateInput(string name, RectTransform parent, string placeholderValue, Vector2 offsetMin, Vector2 offsetMax, bool multiline) { GameObject item = new(name + " Input", typeof(RectTransform), typeof(Image), typeof(InputField)); item.transform.SetParent(parent, false); item.GetComponent<Image>().color = Color.white; AddOutline(item, Color.black, new Vector2(1.5f, -1.5f)); SetTopLeftRect(item.GetComponent<RectTransform>(), offsetMin, offsetMax); Text text = CreateStretchText("Text", item.GetComponent<RectTransform>(), string.Empty, 24, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Vector2(14f, 8f), new Vector2(-14f, -8f), Color.black); Text placeholder = CreateStretchText("Placeholder", item.GetComponent<RectTransform>(), placeholderValue, 19, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, new Vector2(14f, 8f), new Vector2(-14f, -8f), new Color(.36f, .36f, .36f, 1f)); InputField input = item.GetComponent<InputField>(); input.textComponent = text; input.placeholder = placeholder; input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine; return input; }
    private static void AddOutline(GameObject target, Color color, Vector2 distance) { Outline outline = target.AddComponent<Outline>(); outline.effectColor = color; outline.effectDistance = distance; }
    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) { rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
    private static void SetTopLeftRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax) { rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f); rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
}

public static class CommunitySceneRequest
{
    private static string requestedApiBaseUrl;
    private static CommunityPostDraft requestedDraft;

    public static void Open(string apiBaseUrl) { requestedApiBaseUrl = apiBaseUrl; requestedDraft = null; }
    public static void OpenComposerForLevel(string apiBaseUrl, string title, string levelJson)
    {
        requestedApiBaseUrl = apiBaseUrl;
        requestedDraft = new CommunityPostDraft { title = title, level = levelJson };
    }
    public static string Consume(string fallback) { string result = string.IsNullOrWhiteSpace(requestedApiBaseUrl) ? fallback : requestedApiBaseUrl; requestedApiBaseUrl = string.Empty; return result; }
    public static CommunityPostDraft ConsumeDraft() { CommunityPostDraft result = requestedDraft; requestedDraft = null; return result; }
}

public sealed class CommunityPostDraft
{
    public string title;
    public string body;
    public string level;
}
