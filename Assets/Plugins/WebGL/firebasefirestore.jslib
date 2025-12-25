mergeInto(LibraryManager.library, {
    
    SetDocument: function(collectionPath, documentId, jsonData, objectName, callbackSuccess, callbackError) {
        const _collectionPath = UTF8ToString(collectionPath);
        const _documentId = UTF8ToString(documentId);
        const _jsonData = UTF8ToString(jsonData);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        const data = JSON.parse(_jsonData);
        
        firebase.firestore()
            .collection(_collectionPath)
            .doc(_documentId)
            .set(data, { merge: true })
            .then(() => {
                window.unityInstance.SendMessage(_objectName, _callbackSuccess, "success");
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
            });
    },
    
    GetDocument: function(collectionPath, documentId, objectName, callbackSuccess, callbackError) {
        const _collectionPath = UTF8ToString(collectionPath);
        const _documentId = UTF8ToString(documentId);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        firebase.firestore()
            .collection(_collectionPath)
            .doc(_documentId)
            .get()
            .then((doc) => {
                if (doc.exists) {
                    window.unityInstance.SendMessage(_objectName, _callbackSuccess, JSON.stringify(doc.data()));
                } else {
                    window.unityInstance.SendMessage(_objectName, _callbackError, "Document not found");
                }
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
            });
    },
    
    AddDocument: function(collectionPath, jsonData, objectName, callbackSuccess, callbackError) {
        const _collectionPath = UTF8ToString(collectionPath);
        const _jsonData = UTF8ToString(jsonData);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        const data = JSON.parse(_jsonData);
        
        firebase.firestore()
            .collection(_collectionPath)
            .add(data)
            .then((docRef) => {
                window.unityInstance.SendMessage(_objectName, _callbackSuccess, docRef.id);
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
            });
    },
    
    SetDocumentInSubcollection: function(path, documentId, jsonData, objectName, callbackSuccess, callbackError) {
        const _path = UTF8ToString(path);
        const _documentId = UTF8ToString(documentId);
        const _jsonData = UTF8ToString(jsonData);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        const data = JSON.parse(_jsonData);
        
        firebase.firestore()
            .collection(_path)
            .doc(_documentId)
            .set(data, { merge: true })
            .then(() => {
                window.unityInstance.SendMessage(_objectName, _callbackSuccess, "success");
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
            });
    },
    
    GetCollection: function(collectionPath, objectName, callbackSuccess, callbackError) {
        const _collectionPath = UTF8ToString(collectionPath);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        firebase.firestore()
            .collection(_collectionPath)
            .get()
            .then((querySnapshot) => {
                const docIds = [];
                querySnapshot.forEach((doc) => {
                    docIds.push(doc.id);
                });
                window.unityInstance.SendMessage(_objectName, _callbackSuccess, JSON.stringify(docIds));
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
            });
    },
    
    DocumentExists: function(collectionPath, documentId, objectName, callbackSuccess, callbackError) {
        const _collectionPath = UTF8ToString(collectionPath);
        const _documentId = UTF8ToString(documentId);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        firebase.firestore()
            .collection(_collectionPath)
            .doc(_documentId)
            .get()
            .then((doc) => {
                window.unityInstance.SendMessage(_objectName, _callbackSuccess, doc.exists ? "true" : "false");
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
            });
    },
    
    GetCollectionWithData: function(collectionPath, objectName, callbackSuccess, callbackError) {
        const _collectionPath = UTF8ToString(collectionPath);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        firebase.firestore()
            .collection(_collectionPath)
            .get()
            .then((querySnapshot) => {
                const documents = [];
                querySnapshot.forEach((doc) => {
                    documents.push({
                        id: doc.id,
                        data: doc.data()
                    });
                });
                window.unityInstance.SendMessage(_objectName, _callbackSuccess, JSON.stringify(documents));
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
            });
    }
});
