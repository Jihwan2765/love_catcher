"""관리 도구의 Google Application Default Credentials 인증."""
import google.auth
from google.auth.transport.requests import Request

_credentials = None


def authorization_header(project_id):
    global _credentials
    if _credentials is None:
        _credentials, detected_project = google.auth.default(
            scopes=["https://www.googleapis.com/auth/datastore"]
        )
        if detected_project and detected_project != project_id:
            raise RuntimeError(
                f"ADC 프로젝트 {detected_project}와 대상 {project_id}가 다릅니다."
            )
    if not _credentials.valid:
        _credentials.refresh(Request())
    return "Bearer " + _credentials.token
