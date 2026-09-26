"""기존 참가자의 정규화 인스타 인덱스와 이미 뽑힌 프로필 잠금을 만든다.

기본은 미리보기. 중복 핸들이 있으면 어떤 문서도 쓰지 않는다.
ADC로 인증하며 개인 키 파일을 저장소에 두지 않는다.
"""
import argparse
import re
import firebase_admin
from firebase_admin import credentials, firestore


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-id", required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    firebase_admin.initialize_app(credentials.ApplicationDefault(), {"projectId": args.project_id})
    db = firestore.client()
    by_handle = {}
    problems = []
    for snap in db.collection("Participants").stream():
        data = snap.to_dict() or {}
        handle = str(data.get("insta") or data.get("instaId") or "").strip().lstrip("@").lower()
        if not re.fullmatch(r"[a-z0-9._]{1,30}", handle):
            problems.append(f"{snap.id}: 인스타 핸들 확인 필요")
            continue
        by_handle.setdefault(handle, []).append((snap.id, bool(data.get("isPicked"))))
    for handle, people in by_handle.items():
        if len(people) != 1:
            problems.append(f"{handle}: 참가자 문서 {len(people)}개가 중복됨")
    if problems:
        print("운영진이 중복과 누락을 수동 정리해야 합니다. 변경하지 않았습니다.")
        for issue in problems:
            print(" -", issue)
        raise SystemExit(1)
    actions = []
    for handle, ((key, picked),) in by_handle.items():
        index = db.collection("ParticipantKeys").document("insta_" + handle)
        current = index.get()
        if current.exists and current.to_dict().get("participantKey") != key:
            raise SystemExit(f"{handle}: 인덱스가 다른 참가자({current.to_dict()})를 가리킵니다.")
        if not current.exists:
            actions.append((index, {"participantKey": key}))
        if picked:
            claim = db.collection("ProfileClaims").document("insta_" + handle)
            existing = claim.get()
            if existing.exists and existing.to_dict().get("targetKey") != key:
                raise SystemExit(f"{handle}: 기존 프로필 잠금이 다른 문서를 가리킵니다.")
            if not existing.exists:
                actions.append((claim, {"targetKey": key, "kind": "legacy_migration"}))
    print(f"참가자 {len(by_handle)}명, 추가 인덱스/잠금 {len(actions)}건")
    if not args.apply:
        print("미리보기입니다. 점검 후 --apply를 붙여 실행하세요.")
        return
    for ref, data in actions:
        ref.create(data)
    print("마이그레이션 완료. 기존 참가자·시도 횟수·isPicked는 수정하지 않았습니다.")


if __name__ == "__main__":
    main()
