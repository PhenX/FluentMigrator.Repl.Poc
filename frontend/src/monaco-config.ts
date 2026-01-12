import * as monaco from "monaco-editor";
import editorWorker from "monaco-editor/esm/vs/editor/editor.worker?worker";

window["MonacoEnvironment"] = {
  getWorker(_, label) {
    return new editorWorker();
  },
};

export default monaco;
