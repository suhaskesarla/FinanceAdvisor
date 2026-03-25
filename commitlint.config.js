module.exports = {
  extends: ['@commitlint/config-conventional'],
  rules: {
    // Types allowed in this repo
    'type-enum': [
      2,
      'always',
      ['feat', 'fix', 'chore', 'refactor', 'test', 'docs', 'perf'],
    ],

    // Scopes must match a project or concern
    'scope-enum': [
      2,
      'always',
      ['api', 'services', 'core', 'worker', 'infra', 'ci'],
    ],

    // Description rules
    'subject-max-length': [2, 'always', 72],
    'subject-case': [2, 'always', 'lower-case'],
    'subject-full-stop': [2, 'never', '.'],
    'subject-empty': [2, 'never'],

    // Scope is required
    'scope-empty': [2, 'never'],

    // Type is required
    'type-empty': [2, 'never'],
    'type-case': [2, 'always', 'lower-case'],
  },
};
