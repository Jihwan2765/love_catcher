"""운영자가 ADC 권한으로 Firebase 스태프 클레임을 부여한다.

기본은 미리보기다. 계정 비밀번호나 서비스 계정 키를 인자로 받지 않는다.
"""
import argparse
import firebase_admin
from firebase_admin import auth, credentials


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-id", required=True)
    target = parser.add_mutually_exclusive_group(required=True)
    target.add_argument("--email", help="Firebase Authentication의 스태프 이메일")
    target.add_argument("--uid", help="Firebase Authentication 사용자 UID")
    parser.add_argument("--admin", action="store_true", help="재고 수정을 위한 boothAdmin 권한")
    parser.add_argument("--apply", action="store_true", help="실제로 클레임 변경")
    args = parser.parse_args()

    firebase_admin.initialize_app(credentials.ApplicationDefault(), {"projectId": args.project_id})
    user = auth.get_user(args.uid) if args.uid else auth.get_user_by_email(args.email)
    claims = dict(user.custom_claims or {})
    claims["boothStaff"] = True
    if args.admin:
        claims["boothAdmin"] = True
    print(f"project={args.project_id} uid={user.uid} email={user.email} claims={claims}")
    if not args.apply:
        print("미리보기입니다. --apply를 붙이면 변경합니다.")
        return
    auth.set_custom_user_claims(user.uid, claims)
    print("클레임을 저장했습니다. 해당 계정은 재로그인하여 새 ID 토큰을 받아야 합니다.")


if __name__ == "__main__":
    main()
