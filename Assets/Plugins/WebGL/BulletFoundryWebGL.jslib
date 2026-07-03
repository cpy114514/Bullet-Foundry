mergeInto(LibraryManager.library, {
  BulletFoundryQuitWebPage: function () {
    try {
      if (typeof window !== "undefined") {
        if (window.history && window.history.length > 1) {
          window.history.back();
          return;
        }

        window.close();

        window.setTimeout(function () {
          if (!window.closed && window.location) {
            window.location.href = "about:blank";
          }
        }, 100);
      }
    } catch (error) {
      if (typeof console !== "undefined" && console.warn) {
        console.warn("BulletFoundryQuitWebPage failed:", error);
      }
    }
  }
});
