# Remediation commits

Local branch `feature/capstone-remediation`, from baseline
`bdc07caeeb37c31ca7b524b7ef45cde5824a783c` through the final test-runner revision
`d6c2e9d1f576754e9635f948bd814f98605d8c5e`.

The closing documentation commit does not alter executable source. Its hash
is reported separately in the final handoff. No commit was pushed or merged
by this task. This list contains 74 commits before that documentation commit.

| Commit | Subject |
|---|---|
| 0591182c4f079e4756e2f2e7fc3b2f7ce5041423 | docs(remediation): record branch audit and failing baseline |
| ca81bf8a95b3b287f484807cd296e2466766dda2 | fix(reports): isolate review gate and cache month patterns |
| 87cb905c86f3aa80638cd5fd00d032d9ea4c25f9 | fix(web): remove ref access from clarification render |
| 0bc5701c377f6311ca3ed45eb5afb6ca555ef98d | docs(evidence): preserve remediation baseline failures and scope |
| 9b143d12e13864c351a0e2defe54ea8b8b65f122 | fix(web): guard clarification navigation and await catalog options |
| 7a4d52de13463aaed4d069b74347dbc224b7d462 | docs(evidence): record phase one results and unresolved web tests |
| 008f3231fb75973b27d6186a627c46701a344d4c | test(web): fix analytics filter fixture |
| 3e166f405ec3026dbc41ab57b21d1883df227d82 | test(web): query clarification filter by placeholder |
| bb3f127e714043bf1d8eddfbcd986164d528b420 | test(web): bound unit test workers |
| 0494ec4fbaa79ecefb6428a79f6fbb67eab743b9 | docs(evidence): record passing bounded web regression checks |
| 2aa2a2c9287111ebeb0ac130258618fd718b2027 | feat(web): add read-only Supervisor form review |
| 8aca6eae9b2ef153c71efd3a8208431b62dd51ed | fix(auth): scope operational read access |
| 6ed6bca12b83acf5520963d12e8fa358c0bb83ab | test(web): cover operational quick search access |
| a82ab836120d79b12aec97ee715145fd7dbdbf0a | test(auth): import operational read fixtures |
| f0ea64305862a54df0abdb6b18cae060438cac57 | test(web): guard form review test navigation |
| 01dfe0b0d4758d26d7bed3ba315881758b67e4d1 | docs(evidence): record scoped operational authorization checks |
| 7293a8a1e9194b4f93bc74ff975249c7b403b34f | test(compliance): distinguish server receipt timestamps |
| ca2b312a370b93340ebc0321e11dbd03bd4de805 | docs(analytics): clarify completion timestamp meaning |
| 2f2e4b07c2059a37dacf12040c5a70c110b99c34 | docs(compliance): define server-recorded completion clock |
| 6431418b8cf963199080ea3cc50b977e20c47a5d | fix(clients): clarify offline completion timestamps |
| d9233894da1ada57826378201851728ff22ffa37 | test(mobile): pump stale-sync widget test |
| a6cf6e52fef0d01616f2276b4f41d015dcacfda2 | test(mobile): avoid settling active sync animation |
| 3863851d0b35a2dd2b5a1cbd1b4d5a8489863aa4 | test(mobile): isolate stale schedule sync responses |
| 47d6224e063d6c51518b7d7832dc2453467b0875 | test(mobile): scroll conflict action into view |
| 6fe7b02012aeb9214c9d68f15568b8e4a0f71662 | test(mobile): size conflict dialog viewport |
| 100007f2bfb4eddf37298ac62a72ef334d659ed2 | test(mobile): normalize conflict test viewport |
| 0087179d1741ca28fa563f1b3773783ce4840980 | test(mobile): assert acknowledgement time zone |
| f050c2114bc39943ecdb3daf34a53646abdfe9a0 | fix(analytics): remove implicit reviewed NLA shortcuts |
| 6a22efbada8fb0246ae3a57c2c685ba1b5883796 | test(analytics): verify production interpreter boundary |
| 9ba8f6eb50de8afacc3836d96958f9044a725216 | docs(evidence): record completion clock and offline recovery checks |
| 80c16f24bf3617e86696cc40fb4e0386842a517c | test(analytics): block provider calls in production guard test |
| 8ecf28578aa13cd7f7f118cdb9fdcf6216c6f99b | test(analytics): cover configured development provider route |
| b8f5522206dd7722a6649d12fc72da73f421c087 | test(analytics): fix test configuration disposal |
| 1100ab6047b430ec6c77ecda9e14f0bd4fca3cff | fix(mobile): preserve offline work on scope failure |
| d25e5ce69a82ad70005a48a7d304c67234cd5883 | fix(mobile): stop offline sync on account changes |
| ce3af8efa5fe624f243401952d7c39e86bc6ff7d | test(mobile): retain drafts after secure storage errors |
| e5d739c09aa054a002e4a7736160269326217848 | test(mobile): keep account-scope fixture online |
| 3b895e3970a9565776aa41b8fd3ff833b94870f5 | fix(mobile): simplify scope gate error lookup |
| 42148b081e5e7ef9935ac96866dba5f56ce4820c | feat(mobile): show acknowledgement capture account |
| a63428211d76919978fb42683bff92dbdec597b7 | feat(web): show acknowledgement capture account |
| e9a2f831b130cc1bca1cd357f2c2d8b07e0344a5 | test(forms): cover acknowledgement signature and concurrency |
| 41b988e77f0a4e6acb327cbcc0bfa7b4cb5fb9ab | test(forms): fix sql acknowledgement race fixture |
| 6b28909cdd29ec067c6a423f07c339301f71469e | fix(web): show acknowledgement time in Manila |
| 83d47c88f03343eceaf729817cdb6e4e657ec6fa | test(analytics): verify current asset reporting metadata |
| 47d2208344e9b9f66e1ec6cb9e265b38dbb25236 | fix(mobile): label current asset details |
| 892d950a5359c837e1676ad1debfb25579875149 | fix(web): label current asset metadata |
| 4d4ad5c8dde796c7dfe45477d5d1eab279d1d0d0 | test(analytics): seed complete acknowledgement metadata |
| 589bba8f9e5aa8929be17312c54d7301c354f37f | test(analytics): match normalized report grouping |
| 55cf3b22c24de114938790e6cc61cf23d00154f7 | docs(evidence): record NLA receipt and metadata remediation |
| e500dcb3f141f633839d67c1da1f1ea45af21e81 | feat(mobile): drain paged maintenance forms |
| 1dd8a75c0ec5027e9815b8f5d52e8bd0f0e036a2 | docs(evidence): clarify routing and focused command provenance |
| ee4e409c14285d7f74d51e2bda6b7ab13d7ce954 | feat(api): paginate authorized PM form registry |
| 62b87c1afc60d2932fe3438d1228732e77efe37a | test(api): verify SQL form pagination compatibility |
| 03ba54bd711f176130252dd5e729957b5d49cce1 | test(api): use valid unassigned schedule fixture |
| e6b49f77f8a9d6c473e73894d8d3a975fcc7f077 | test(api): assert interpreted report period |
| 34ea9f186510d59c89b4532f7862c0e7c8f2da36 | feat(web): paginate maintenance form review |
| c8288a4dad8f7258a6f78739170e974be123bdb5 | test(web): fix paged registry assertions |
| 30beffbc152e5d15cf51274b534b54682c6b62c4 | fix(web): use server count for form pagination |
| 629ec6e001f2123ddab1d98b4ca5ca145577aa90 | style(web): format pagination contract check |
| 2d3925540a8146df42a5241d88e3f06c6a8fcda2 | style(web): format generated OpenAPI snapshot |
| 77640816cf433423b9a282bdcdda4b302ea1c797 | docs(evidence): record pagination and bounded scale checks |
| f42a5374841502a653f8dce32fa7c35ccd05a144 | feat(mobile): set stable Android package identity |
| 8a9d2e215635943935b39ae4509514bf0ed74a14 | fix(mobile): clarify offline and QR verification limits |
| c0036cebe251c33965e8ff4424a50b4cf42fa31b | test(mobile): drop text-only copy assertions |
| 8cea97af614bdcb9c2bfd9016261016694a5d5c4 | docs(mobile): remove location verification claim |
| 791d396f6d39a7447e10b27e65439cb581dc7b99 | docs(evidence): record mobile release boundary checks |
| f0f8a34e809ac364adc6e1dd389de1ae4afbb497 | docs(research): reconcile capstone evidence and revision proposal |
| bf5df8cf925a1920c8ed15557c0efe799679eb62 | docs(operations): record security review and institutional plan |
| 32d2216d16019c455959879b10961fe96383602e | fix(staging): update migration preflight |
| fc06ad5937756ae7d01e1e54262c0e57dbc01f6c | test(schedules): fix reassignment lock races |
| 8b20bf9069a428b2054603213692720953b925dc | test(sql): repair staging and role fixtures |
| 28a074f5a393f7d8c63f88099e5c7290767b346c | test(staging): isolate schema guard checks |
| ac5787ce79abd8ecf5d2646ab5a9bd514cdc36f6 | test(web): align e2e form mocks with paged API |
| d6c2e9d1f576754e9635f948bd814f98605d8c5e | test(web): use process-isolated Vitest workers |
