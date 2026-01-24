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
    },

    // ========================================================================
    // NEW: Increment-based room stats update
    // This properly accumulates time and visit counts instead of overwriting
    // ========================================================================
    
    UpdateRoomStatsIncrement: function(collectionPath, documentId, timeToAdd, visitsToAdd, objectName, callbackSuccess, callbackError) {
        const _collectionPath = UTF8ToString(collectionPath);
        const _documentId = UTF8ToString(documentId);
        const _timeToAdd = timeToAdd; // float passed directly
        const _visitsToAdd = visitsToAdd; // int passed directly
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        const docRef = firebase.firestore()
            .collection(_collectionPath)
            .doc(_documentId);
        
        // Use set with merge to create document if it doesn't exist,
        // combined with FieldValue.increment for atomic updates
        docRef.set({
            timeSpent: firebase.firestore.FieldValue.increment(_timeToAdd),
            visitCount: firebase.firestore.FieldValue.increment(_visitsToAdd),
            lastVisit: firebase.firestore.FieldValue.serverTimestamp()
        }, { merge: true })
        .then(() => {
            window.unityInstance.SendMessage(_objectName, _callbackSuccess, "success");
        })
        .catch((error) => {
            window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
        });
    },

    // ========================================================================
    // NEW: Update progress summary with increment for score
    // ========================================================================
    
    UpdateProgressSummaryIncrement: function(collectionPath, documentId, scoreToAdd, cardsToAdd, lastCardId, objectName, callbackSuccess, callbackError) {
        const _collectionPath = UTF8ToString(collectionPath);
        const _documentId = UTF8ToString(documentId);
        const _scoreToAdd = scoreToAdd;
        const _cardsToAdd = cardsToAdd;
        const _lastCardId = UTF8ToString(lastCardId);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        const updateData = {
            lastUpdated: firebase.firestore.FieldValue.serverTimestamp()
        };
        
        // Only add increment fields if values are non-zero
        if (_scoreToAdd !== 0) {
            updateData.totalScore = firebase.firestore.FieldValue.increment(_scoreToAdd);
        }
        if (_cardsToAdd !== 0) {
            updateData.totalCardsCollected = firebase.firestore.FieldValue.increment(_cardsToAdd);
        }
        if (_lastCardId && _lastCardId.length > 0) {
            updateData.lastCardFound = _lastCardId;
        }
        
        firebase.firestore()
            .collection(_collectionPath)
            .doc(_documentId)
            .set(updateData, { merge: true })
            .then(() => {
                window.unityInstance.SendMessage(_objectName, _callbackSuccess, "success");
            })
            .catch((error) => {
                window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
            });
    },

    // ========================================================================
    // NEW: Batch write multiple documents atomically
    // ========================================================================
    
    BatchWrite: function(operationsJson, objectName, callbackSuccess, callbackError) {
        const _operationsJson = UTF8ToString(operationsJson);
        const _objectName = UTF8ToString(objectName);
        const _callbackSuccess = UTF8ToString(callbackSuccess);
        const _callbackError = UTF8ToString(callbackError);
        
        try {
            const operations = JSON.parse(_operationsJson);
            const batch = firebase.firestore().batch();
            
            operations.forEach((op) => {
                const docRef = firebase.firestore()
                    .collection(op.collection)
                    .doc(op.documentId);
                
                if (op.type === 'set') {
                    batch.set(docRef, op.data, { merge: op.merge || false });
                } else if (op.type === 'update') {
                    batch.update(docRef, op.data);
                } else if (op.type === 'delete') {
                    batch.delete(docRef);
                }
            });
            
            batch.commit()
                .then(() => {
                    window.unityInstance.SendMessage(_objectName, _callbackSuccess, "success");
                })
                .catch((error) => {
                    window.unityInstance.SendMessage(_objectName, _callbackError, error.message);
                });
        } catch (error) {
            window.unityInstance.SendMessage(_objectName, _callbackError, "JSON parse error: " + error.message);
        }
    }
});
