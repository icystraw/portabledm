var bwIcon = "16-bw.png";
var normalIcons = { "16": "16.png", "48": "48.png", "128": "128.png" };

function sendDownload(downloadItem) {
  chrome.storage.local.get(["enabled"], function (result) {
    var isEnabled = result.hasOwnProperty("enabled") ? result.enabled : true;
    if (!isEnabled) return;
    var startTime = Date.parse(downloadItem.startTime);
    var now = Date.now();
    if (Math.abs(now - startTime) < 1000) {
      var encodedUrl = btoa(downloadItem.finalUrl);
      fetch("http://localhost:13000/" + encodedUrl)
        .then(function (response) {
          if (response.status == 403) {
            chrome.downloads.cancel(downloadItem.id);
          }
        })
        .catch((error) => {
          console.error(error);
        });
    }
  });
}

function toggleIconStatus(tab) {
  chrome.storage.local.get(["enabled"], function (result) {
    var isEnabled = result.hasOwnProperty("enabled") ? !result.enabled : false;
    chrome.storage.local.set({ enabled: isEnabled }, function () {
      setIconStyle(isEnabled);
    });
  });
}

function syncIconStatus(tab) {
  chrome.storage.local.get(["enabled"], function (result) {
    var isEnabled = result.hasOwnProperty("enabled") ? result.enabled : true;
    setIconStyle(isEnabled);
  });
}

function setIconStyle(bEnabled) {
  if (bEnabled) {
    chrome.action.setIcon({ path: normalIcons });
  }
  else {
    chrome.action.setIcon({ path: bwIcon });
  }
}

chrome.tabs.onCreated.addListener(syncIconStatus);
chrome.downloads.onCreated.addListener(sendDownload);
chrome.action.onClicked.addListener(toggleIconStatus);
chrome.webRequest.onBeforeRequest.addListener(
  (details) => {
    console.log('onBeforeRequest', details)
  },
  { urls: ["<all_urls>"] },
);