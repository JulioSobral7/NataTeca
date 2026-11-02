import { Route, Routes } from "react-router-dom";
import "./App.css";
import EstantesPage from "./pages/EstantesPage";
import EstanteViewPage from "./pages/EstanteViewPage";

function App() {
  return (
    <div className="app-shell">
      <Routes>
        <Route path="/" element={<EstantesPage />} />
        <Route path="/estantes/:id" element={<EstanteViewPage />} />
      </Routes>
    </div>
  );
}

export default App;
