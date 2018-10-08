//var baseUrl = 'https://yutbube.blob.core.windows.net/client/index.html';
var baseUrl = 'http://localhost:4200/';

chrome.browserAction.onClicked.addListener(function (tab) {
    chrome.tabs.executeScript(tab.id, { file: 'main.js' }, function (id) {
        if (id == null || id === "") return;

        chrome.tabs.query({ url: baseUrl }, function (tabs) {
            if (tabs.length == 0) {
                chrome.tabs.create({ url: baseUrl + '#' + id });
            } else {
                chrome.tabs.update(tabs[0].id, { url: baseUrl + '#' + id, selected: true });
            }
        });
    });
});