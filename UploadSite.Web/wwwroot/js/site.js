const dropzone = document.querySelector(".dropzone");
const fileInput = document.querySelector("#audio-file");
const dropzoneTitle = document.querySelector("#dropzone-title");
const dropzoneSubtitle = document.querySelector("#dropzone-subtitle");

function updateSelectedFile(file) {
  if (!file || !dropzoneTitle || !dropzoneSubtitle) {
    return;
  }

  dropzoneTitle.textContent = file.name;
  dropzoneSubtitle.textContent = `${Math.max(1, Math.round(file.size / 1024))} KB selected`;
}

function updateSelectedFiles(files) {
  if (!files || files.length === 0 || !dropzoneTitle || !dropzoneSubtitle) {
    return;
  }

  if (files.length === 1) {
    updateSelectedFile(files[0]);
    return;
  }

  const totalKb = Array.from(files).reduce((sum, file) => sum + Math.round(file.size / 1024), 0);
  dropzoneTitle.textContent = `${files.length} files selected`;
  dropzoneSubtitle.textContent = `${Math.max(1, totalKb)} KB total`;
}

if (dropzone && fileInput) {
  ["dragenter", "dragover"].forEach((eventName) => {
    dropzone.addEventListener(eventName, (event) => {
      event.preventDefault();
      dropzone.classList.add("dragover");
    });
  });

  ["dragleave", "drop"].forEach((eventName) => {
    dropzone.addEventListener(eventName, (event) => {
      event.preventDefault();
      dropzone.classList.remove("dragover");
    });
  });

  dropzone.addEventListener("drop", (event) => {
    const files = event.dataTransfer?.files;
    if (!files || files.length === 0) {
      return;
    }

    fileInput.files = files;
    updateSelectedFiles(files);
  });

  fileInput.addEventListener("change", () => {
    const files = fileInput.files;
    updateSelectedFiles(files);
  });
}
