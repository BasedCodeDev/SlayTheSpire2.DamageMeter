# Project workflow

After completing implementation that affects the mod, read and follow
[spires-live-deploy](.agents/skills/spires-live-deploy/SKILL.md) before the final
response. This is implicit: the user does not need to invoke the skill or ask
for deployment. Apply it once after relevant checks pass.

Skip planning, questions, documentation-only and test-only work, incomplete or
failing implementations, and work where the user explicitly asks to skip deployment.
Preserve unrelated edits. Deployment builds the current checkout, including
uncommitted changes; never commit, pull, push or bump versions automatically.
