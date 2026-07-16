using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class CommunityPostSummary
{
    public string id;
    public string type;
    public string title;
    public string author;
    public string bodyPreview;
    public string mediaUrl;
    public string createdAt;
    public bool hasLevel;
    public int spawnCount;
}

[Serializable]
public sealed class CommunityPostListResponse
{
    public CommunityPostSummary[] posts = Array.Empty<CommunityPostSummary>();
}

[Serializable]
public sealed class CommunityPostDetail
{
    public string id;
    public string type;
    public string title;
    public string author;
    public string body;
    public string mediaUrl;
    public string createdAt;
    public bool hasLevel;
    public int spawnCount;
    public string level;
}

[Serializable]
public sealed class CommunityPostPublishRequest
{
    public string type;
    public string title;
    public string author;
    public string body;
    public string mediaUrl;
    public string level;
}

[Serializable]
public sealed class CommunityPostPublishResponse
{
    public string id;
    public string message;
}

[Serializable]
public sealed class CommunityImageUploadResponse
{
    public string mediaUrl;
}

public static class CommunityLevelApi
{
    public static bool TryGetBaseUrl(string value, out string baseUrl)
    {
        baseUrl = value?.Trim().TrimEnd('/') ?? string.Empty;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            baseUrl = string.Empty;
            return false;
        }

        return true;
    }

    public static IEnumerator GetPosts(
        string configuredBaseUrl,
        Action<CommunityPostListResponse> success,
        Action<string> failure)
    {
        if (!TryGetBaseUrl(configuredBaseUrl, out string baseUrl))
        {
            failure?.Invoke("Community server URL is not configured.");
            yield break;
        }

        using UnityWebRequest request = UnityWebRequest.Get(baseUrl + "/api/posts");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            failure?.Invoke(RequestError(request));
            yield break;
        }

        CommunityPostListResponse response;
        try
        {
            response = JsonUtility.FromJson<CommunityPostListResponse>(request.downloadHandler.text);
        }
        catch (Exception exception)
        {
            failure?.Invoke("Invalid community feed: " + exception.Message);
            yield break;
        }

        if (response == null)
        {
            failure?.Invoke("Community server returned an empty response.");
            yield break;
        }

        response.posts ??= Array.Empty<CommunityPostSummary>();
        success?.Invoke(response);
    }

    public static IEnumerator GetPost(
        string configuredBaseUrl,
        string postId,
        Action<CommunityPostDetail> success,
        Action<string> failure)
    {
        if (!TryGetBaseUrl(configuredBaseUrl, out string baseUrl))
        {
            failure?.Invoke("Community server URL is not configured.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(postId))
        {
            failure?.Invoke("Community post id is empty.");
            yield break;
        }

        using UnityWebRequest request = UnityWebRequest.Get(baseUrl + "/api/posts/" + UnityWebRequest.EscapeURL(postId));
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            failure?.Invoke(RequestError(request));
            yield break;
        }

        CommunityPostDetail response;
        try
        {
            response = JsonUtility.FromJson<CommunityPostDetail>(request.downloadHandler.text);
        }
        catch (Exception exception)
        {
            failure?.Invoke("Invalid community post: " + exception.Message);
            yield break;
        }

        if (response == null)
        {
            failure?.Invoke("Community server returned an empty post.");
            yield break;
        }

        success?.Invoke(response);
    }

    public static IEnumerator PublishPost(
        string configuredBaseUrl,
        CommunityPostPublishRequest payload,
        Action<CommunityPostPublishResponse> success,
        Action<string> failure)
    {
        if (!TryGetBaseUrl(configuredBaseUrl, out string baseUrl))
        {
            failure?.Invoke("Set Community API URL first.");
            yield break;
        }

        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        using UnityWebRequest request = new(baseUrl + "/api/posts", UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            failure?.Invoke(RequestError(request));
            yield break;
        }

        CommunityPostPublishResponse response;
        try
        {
            response = JsonUtility.FromJson<CommunityPostPublishResponse>(request.downloadHandler.text);
        }
        catch (Exception exception)
        {
            failure?.Invoke("Invalid publish response: " + exception.Message);
            yield break;
        }

        success?.Invoke(response ?? new CommunityPostPublishResponse());
    }

    public static IEnumerator UploadImage(
        string configuredBaseUrl,
        byte[] bytes,
        string fileName,
        Action<string> success,
        Action<string> failure)
    {
        if (!TryGetBaseUrl(configuredBaseUrl, out string baseUrl))
        {
            failure?.Invoke("Set Community API URL first.");
            yield break;
        }

        if (bytes == null || bytes.Length == 0)
        {
            failure?.Invoke("Choose an image before publishing.");
            yield break;
        }

        string extension = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
        string contentType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(contentType))
        {
            failure?.Invoke("Image must be a PNG, JPG, or WEBP file.");
            yield break;
        }

        using UnityWebRequest request = new(baseUrl + "/api/uploads", UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(bytes),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", contentType);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            failure?.Invoke(RequestError(request));
            yield break;
        }

        CommunityImageUploadResponse response;
        try
        {
            response = JsonUtility.FromJson<CommunityImageUploadResponse>(request.downloadHandler.text);
        }
        catch (Exception exception)
        {
            failure?.Invoke("Invalid image upload response: " + exception.Message);
            yield break;
        }

        if (response == null || string.IsNullOrWhiteSpace(response.mediaUrl))
        {
            failure?.Invoke("Community server did not return an image URL.");
            yield break;
        }

        string mediaUrl = response.mediaUrl.Trim();
        if (mediaUrl.StartsWith("/", StringComparison.Ordinal))
        {
            mediaUrl = baseUrl + mediaUrl;
        }

        success?.Invoke(mediaUrl);
    }

    private static string RequestError(UnityWebRequest request)
    {
        string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        return string.IsNullOrWhiteSpace(body) ? request.error : body;
    }
}
