using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable] public sealed class CommunityPostSummary { public string id; public string type; public string title; public string author; public string bodyPreview; public string mediaUrl; public string createdAt; public bool hasLevel; public int spawnCount; public int commentCount; public int likeCount; }
[Serializable] public sealed class CommunityPostListResponse { public CommunityPostSummary[] posts = Array.Empty<CommunityPostSummary>(); }
[Serializable] public sealed class CommunityComment { public string id; public string author; public string body; public string createdAt; }
[Serializable] public sealed class CommunityCommentListResponse { public CommunityComment[] comments = Array.Empty<CommunityComment>(); }
[Serializable] public sealed class CommunityPostDetail { public string id; public string type; public string title; public string author; public string body; public string mediaUrl; public string createdAt; public bool hasLevel; public int spawnCount; public string level; public CommunityComment[] comments = Array.Empty<CommunityComment>(); public int likeCount; public bool likedByCurrentUser; public bool favoritedByCurrentUser; }
[Serializable] public sealed class CommunityPostPublishRequest { public string type; public string title; public string author; public string body; public string mediaUrl; public string level; }
[Serializable] public sealed class CommunityPostPublishResponse { public string id; public string message; }
[Serializable] public sealed class CommunityImageUploadResponse { public string mediaUrl; }
[Serializable] public sealed class CommunityCredentials { public string username; public string password; }
[Serializable] public sealed class CommunityAuthResponse { public string username; public string token; }
[Serializable] public sealed class CommunityMeResponse { public string username; }
[Serializable] public sealed class CommunityCommentRequest { public string body; }
[Serializable] public sealed class CommunityCommentPublishResponse { public CommunityComment comment; }
[Serializable] public sealed class CommunityLikeResponse { public bool liked; public int likeCount; }
[Serializable] public sealed class CommunityFavoriteResponse { public bool favorited; }

public static class CommunityAccount
{
    private const string TokenKey = "Community.AuthToken";
    private const string UsernameKey = "Community.Username";

    public static string Token => PlayerPrefs.GetString(TokenKey, string.Empty);
    public static string Username => PlayerPrefs.GetString(UsernameKey, string.Empty);
    public static bool IsSignedIn => !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(Username);

    public static void Set(CommunityAuthResponse response)
    {
        PlayerPrefs.SetString(TokenKey, response?.token ?? string.Empty);
        PlayerPrefs.SetString(UsernameKey, response?.username ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.Save();
    }
}

public static class CommunityLevelApi
{
    public static bool TryGetBaseUrl(string value, out string baseUrl)
    {
        baseUrl = value?.Trim().TrimEnd('/') ?? string.Empty;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) { baseUrl = string.Empty; return false; }
        return true;
    }

    public static IEnumerator GetPosts(string configuredBaseUrl, Action<CommunityPostListResponse> success, Action<string> failure) => SendJson<CommunityPostListResponse>(configuredBaseUrl, "/api/posts", UnityWebRequest.kHttpVerbGET, null, false, success, failure);
    public static IEnumerator GetMyPosts(string configuredBaseUrl, Action<CommunityPostListResponse> success, Action<string> failure) => SendJson<CommunityPostListResponse>(configuredBaseUrl, "/api/me/posts", UnityWebRequest.kHttpVerbGET, null, true, success, failure);
    public static IEnumerator GetFavorites(string configuredBaseUrl, Action<CommunityPostListResponse> success, Action<string> failure) => SendJson<CommunityPostListResponse>(configuredBaseUrl, "/api/me/favorites", UnityWebRequest.kHttpVerbGET, null, true, success, failure);
    public static IEnumerator GetPost(string configuredBaseUrl, string id, Action<CommunityPostDetail> success, Action<string> failure) => SendJson<CommunityPostDetail>(configuredBaseUrl, "/api/posts/" + UnityWebRequest.EscapeURL(id), UnityWebRequest.kHttpVerbGET, null, false, success, failure);
    public static IEnumerator GetComments(string configuredBaseUrl, string id, Action<CommunityCommentListResponse> success, Action<string> failure) => SendJson<CommunityCommentListResponse>(configuredBaseUrl, "/api/posts/" + UnityWebRequest.EscapeURL(id) + "/comments", UnityWebRequest.kHttpVerbGET, null, false, success, failure);
    public static IEnumerator GetMe(string configuredBaseUrl, Action<CommunityMeResponse> success, Action<string> failure) => SendJson<CommunityMeResponse>(configuredBaseUrl, "/api/auth/me", UnityWebRequest.kHttpVerbGET, null, true, success, failure);

    public static IEnumerator Register(string configuredBaseUrl, CommunityCredentials value, Action<CommunityAuthResponse> success, Action<string> failure) => SendJson<CommunityAuthResponse>(configuredBaseUrl, "/api/auth/register", UnityWebRequest.kHttpVerbPOST, value, false, success, failure);
    public static IEnumerator Login(string configuredBaseUrl, CommunityCredentials value, Action<CommunityAuthResponse> success, Action<string> failure) => SendJson<CommunityAuthResponse>(configuredBaseUrl, "/api/auth/login", UnityWebRequest.kHttpVerbPOST, value, false, success, failure);
    public static IEnumerator PublishPost(string configuredBaseUrl, CommunityPostPublishRequest value, Action<CommunityPostPublishResponse> success, Action<string> failure) => SendJson<CommunityPostPublishResponse>(configuredBaseUrl, "/api/posts", UnityWebRequest.kHttpVerbPOST, value, true, success, failure);
    public static IEnumerator UpdatePost(string configuredBaseUrl, string postId, CommunityPostPublishRequest value, Action<CommunityPostPublishResponse> success, Action<string> failure) => SendJson<CommunityPostPublishResponse>(configuredBaseUrl, "/api/posts/" + UnityWebRequest.EscapeURL(postId), UnityWebRequest.kHttpVerbPUT, value, true, success, failure);
    public static IEnumerator ToggleLike(string configuredBaseUrl, string postId, Action<CommunityLikeResponse> success, Action<string> failure) => SendJson<CommunityLikeResponse>(configuredBaseUrl, "/api/posts/" + UnityWebRequest.EscapeURL(postId) + "/like", UnityWebRequest.kHttpVerbPOST, null, true, success, failure);
    public static IEnumerator ToggleFavorite(string configuredBaseUrl, string postId, Action<CommunityFavoriteResponse> success, Action<string> failure) => SendJson<CommunityFavoriteResponse>(configuredBaseUrl, "/api/posts/" + UnityWebRequest.EscapeURL(postId) + "/favorite", UnityWebRequest.kHttpVerbPOST, null, true, success, failure);
    public static IEnumerator PublishComment(string configuredBaseUrl, string postId, CommunityCommentRequest value, Action<CommunityCommentPublishResponse> success, Action<string> failure) => SendJson<CommunityCommentPublishResponse>(configuredBaseUrl, "/api/posts/" + UnityWebRequest.EscapeURL(postId) + "/comments", UnityWebRequest.kHttpVerbPOST, value, true, success, failure);
    public static IEnumerator DeleteComment(string configuredBaseUrl, string postId, string commentId, Action<CommunityPostPublishResponse> success, Action<string> failure) => SendJson<CommunityPostPublishResponse>(configuredBaseUrl, "/api/posts/" + UnityWebRequest.EscapeURL(postId) + "/comments/" + UnityWebRequest.EscapeURL(commentId), UnityWebRequest.kHttpVerbDELETE, null, true, success, failure);
    public static IEnumerator DeletePost(string configuredBaseUrl, string postId, Action<CommunityPostPublishResponse> success, Action<string> failure) => SendJson<CommunityPostPublishResponse>(configuredBaseUrl, "/api/posts/" + UnityWebRequest.EscapeURL(postId), UnityWebRequest.kHttpVerbDELETE, null, true, success, failure);

    public static IEnumerator UploadImage(string configuredBaseUrl, byte[] bytes, string fileName, Action<string> success, Action<string> failure)
    {
        if (!TryGetBaseUrl(configuredBaseUrl, out string baseUrl)) { failure?.Invoke("Set Community API URL first."); yield break; }
        if (!CommunityAccount.IsSignedIn) { failure?.Invoke("Sign in before uploading an image."); yield break; }
        if (bytes == null || bytes.Length == 0) { failure?.Invoke("Image is empty."); yield break; }
        string contentType = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => string.Empty };
        if (string.IsNullOrEmpty(contentType)) { failure?.Invoke("Image must be a PNG, JPG, or WEBP file."); yield break; }
        using UnityWebRequest request = new(baseUrl + "/api/uploads", UnityWebRequest.kHttpVerbPOST) { uploadHandler = new UploadHandlerRaw(bytes), downloadHandler = new DownloadHandlerBuffer() };
        request.SetRequestHeader("Content-Type", contentType); AddAuthorization(request, true);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success) { failure?.Invoke(RequestError(request)); yield break; }
        CommunityImageUploadResponse response;
        try { response = JsonUtility.FromJson<CommunityImageUploadResponse>(request.downloadHandler.text); } catch (Exception exception) { failure?.Invoke("Invalid image upload response: " + exception.Message); yield break; }
        if (response == null || string.IsNullOrWhiteSpace(response.mediaUrl)) { failure?.Invoke("Community server did not return an image URL."); yield break; }
        success?.Invoke(response.mediaUrl.Trim().StartsWith("/", StringComparison.Ordinal) ? baseUrl + response.mediaUrl.Trim() : response.mediaUrl.Trim());
    }

    private static IEnumerator SendJson<T>(string configuredBaseUrl, string path, string method, object payload, bool needsAuth, Action<T> success, Action<string> failure) where T : class
    {
        if (!TryGetBaseUrl(configuredBaseUrl, out string baseUrl)) { failure?.Invoke("Community server URL is not configured."); yield break; }
        if (needsAuth && !CommunityAccount.IsSignedIn) { failure?.Invoke("Sign in to continue."); yield break; }
        using UnityWebRequest request = new(baseUrl + path, method) { downloadHandler = new DownloadHandlerBuffer() };
        if (payload != null) { request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload))); request.SetRequestHeader("Content-Type", "application/json; charset=utf-8"); }
        AddAuthorization(request, needsAuth);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success) { if (request.responseCode == 401) CommunityAccount.Clear(); failure?.Invoke(RequestError(request)); yield break; }
        T response;
        try { response = JsonUtility.FromJson<T>(request.downloadHandler.text); } catch (Exception exception) { failure?.Invoke("Invalid server response: " + exception.Message); yield break; }
        success?.Invoke(response);
    }

    private static void AddAuthorization(UnityWebRequest request, bool needsAuth) { if (needsAuth && CommunityAccount.IsSignedIn) request.SetRequestHeader("Authorization", "Bearer " + CommunityAccount.Token); }
    private static string RequestError(UnityWebRequest request) { string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty; return string.IsNullOrWhiteSpace(body) ? request.error : body; }
}
