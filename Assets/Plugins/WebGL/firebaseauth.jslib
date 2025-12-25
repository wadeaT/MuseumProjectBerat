mergeInto(LibraryManager.library, {
    
    SignInAnonymously: function(objectName, callbackSuccess, callbackError) {
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        firebase.auth().signInAnonymously()
            .then((userCredential) => {
                window.unityInstance.SendMessage(_objectName, _callbackSuccess, userCredential.user.uid);
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, JSON.stringify({
                    code: error.code, 
                    message: error.message
                }));
            });
    },
    
    GetCurrentUserId: function() {
        const user = firebase.auth().currentUser;
        if (user) {
            const userId = user.uid;
            const bufferSize = lengthBytesUTF8(userId) + 1;
            const buffer = _malloc(bufferSize);
            stringToUTF8(userId, buffer, bufferSize);
            return buffer;
        }
        return null;
    }
});
