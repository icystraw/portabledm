var bwIcon = "16-bw.png";
var normalIcons = {"16": "16.png", "48": "48.png", "128": "128.png"};

function sendDownload(downloadItem) {
  console.log(downloadItem.finalUrl);
  console.log(downloadItem.startTime);

  chrome.storage.local.get(["enabled"], function(result) {
    console.log(result.hasOwnProperty("enabled"));
    var isEnabled = true;
    if (result.hasOwnProperty("enabled")) {
      isEnabled = result.enabled;
    }
    console.log(isEnabled);
    if (isEnabled == false) return;
    var startTime = Date.parse(downloadItem.startTime);
    var now = Date.now();
    if (Math.abs(now - startTime) < 1000) {
      var encodedUrl = btoa(downloadItem.finalUrl);
      fetch("http://localhost:13000/" + encodedUrl)
        .then(function(response) {
          console.log(response.status);
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

function setIconStatus(tab)
{
  console.log("icon clicked.");
  chrome.storage.local.get(["enabled"], function(result) {
    console.log("Value currently is " + result.enabled);
    if (result.hasOwnProperty("enabled")) {
      chrome.storage.local.set({enabled: !result.enabled}, function() {
        console.log("Value is set to " + !result.enabled);
        setIconStyle(!result.enabled);
      });
    }
    else {
      chrome.storage.local.set({enabled: false}, function() {
        console.log("Value is set to false.");
        setIconStyle(false);
      });
    }
  });
}

function syncIconStatus(tab) {
  chrome.storage.local.get(["enabled"], function(result) {
    var isEnabled = true;
    if (result.hasOwnProperty("enabled")) {
      isEnabled = result.enabled;
    }
    console.log(isEnabled);
    setIconStyle(isEnabled);
  });
}

function setIconStyle(bEnabled) {
  if (bEnabled == true) {
    chrome.action.setIcon({path: normalIcons});
  }
  else {
    chrome.action.setIcon({path: bwIcon});
  }
}

chrome.tabs.onCreated.addListener(syncIconStatus);
chrome.downloads.onCreated.addListener(sendDownload);
chrome.action.onClicked.addListener(setIconStatus);