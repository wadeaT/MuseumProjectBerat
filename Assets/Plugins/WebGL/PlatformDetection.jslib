mergeInto(LibraryManager.library, {
    
    IsMobileBrowser: function() {
        // Check for touch capability
        var hasTouchScreen = false;
        
        if ("maxTouchPoints" in navigator) {
            hasTouchScreen = navigator.maxTouchPoints > 0;
        } else if ("msMaxTouchPoints" in navigator) {
            hasTouchScreen = navigator.msMaxTouchPoints > 0;
        }
        
        // Check user agent for mobile devices
        var userAgent = navigator.userAgent || navigator.vendor || window.opera;
        var isMobileUA = /android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini|mobile|tablet/i.test(userAgent.toLowerCase());
        
        // Check screen size (mobile typically < 1024px width)
        var isSmallScreen = window.innerWidth <= 1024;
        
        // Consider it mobile if: has touch + (mobile user agent OR small screen)
        var isMobile = hasTouchScreen && (isMobileUA || isSmallScreen);
        
        console.log("[PlatformDetection] Touch: " + hasTouchScreen + ", MobileUA: " + isMobileUA + ", SmallScreen: " + isSmallScreen + " → Mobile: " + isMobile);
        
        return isMobile;
    }
});