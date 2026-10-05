import type { Problem } from '../types';

/** Shows a problem from the API, with every violation exactly as the API reports it. */
export function ProblemMessage({ problem }: { problem: Problem | null }) {
  if (!problem) {
    return null;
  }

  return (
    <div className="problem" role="alert">
      <strong>{problem.title}</strong>
      {problem.detail && <p>{problem.detail}</p>}
      {problem.violations && problem.violations.length > 0 && (
        <ul>
          {problem.violations.map((violation, index) => (
            <li key={index}>
              {violation.target}: {violation.message}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
