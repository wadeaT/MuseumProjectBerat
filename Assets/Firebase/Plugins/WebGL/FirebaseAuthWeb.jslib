mergeInto(LibraryManager.library, {
    
    FirebaseRegisterUser: function(email, password, objectName, callbackMethod) {
        const emailStr = UTF8ToString(email);
        const passwordStr = UTF8ToString(password);
        const objectNameStr = UTF8ToString(objectName);
        const callbackStr = UTF8ToString(callbackMethod);
        
        if (typeof firebase === 'undefined') {
            console.error("Firebase is not loaded!");
            SendMessage(objectNameStr, callbackStr, "ERROR:Firebase not loaded");
            return;
        }
        
        console.log("[JS] Registering user:", emailStr);
        
        firebase.auth().createUserWithEmailAndPassword(emailStr, passwordStr)
            .then((userCredential) => {
                const uid = userCredential.user.uid;
                console.log("[JS] Registration success:", uid);
                SendMessage(objectNameStr, callbackStr, "SUCCESS:" + uid);
            })
            .catch((error) => {
                console.error("[JS] Registration error:", error.message);
                SendMessage(objectNameStr, callbackStr, "ERROR:" + error.message);
            });
    },
    
    FirebaseLoginUser: function(email, password, objectName, callbackMethod) {
        const emailStr = UTF8ToString(email);
        const passwordStr = UTF8ToString(password);
        const objectNameStr = UTF8ToString(objectName);
        const callbackStr = UTF8ToString(callbackMethod);
        
        if (typeof firebase === 'undefined') {
            console.error("Firebase is not loaded!");
            SendMessage(objectNameStr, callbackStr, "ERROR:Firebase not loaded");
            return;
        }
        
        console.log("[JS] Logging in user:", emailStr);
        
        firebase.auth().signInWithEmailAndPassword(emailStr, passwordStr)
            .then((userCredential) => {
                const uid = userCredential.user.uid;
                console.log("[JS] Login success:", uid);
                SendMessage(objectNameStr, callbackStr, "SUCCESS:" + uid);
            })
            .catch((error) => {
                console.error("[JS] Login error:", error.message);
                SendMessage(objectNameStr, callbackStr, "ERROR:" + error.message);
            });
    },
    
    FirebaseGetCurrentUserId: function() {
        if (typeof firebase === 'undefined' || !firebase.auth().currentUser) {
            return null;
        }
        const uid = firebase.auth().currentUser.uid;
        const bufferSize = lengthBytesUTF8(uid) + 1;
        const buffer = _malloc(bufferSize);
        stringToUTF8(uid, buffer, bufferSize);
        return buffer;
    },
    
    FirebaseSignOut: function() {
        if (typeof firebase !== 'undefined') {
            firebase.auth().signOut();
            console.log("[JS] Signed out");
        }
    }
});