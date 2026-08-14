// Copyright PlayProbe.io 2026. All rights reserved

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace PlayProbe.Editor
{
    /// <summary>
    /// Asks the backend whether a share token is usable before the developer finds out the hard way.
    /// <para>
    /// The SDK is a Pro feature and the real gate lives in the <c>sdk-start-session</c> edge
    /// function, which refuses a session outright. Without this check the first sign of a wrong
    /// token, a test with SDK mode off, or a lapsed subscription is a console warning in a build
    /// that has already shipped. One call at setup time turns all three into an editor message with
    /// a button that fixes it.
    /// </para>
    /// </summary>
    internal static class PlayProbeTokenVerifier
    {
        private const string EndpointName = "sdk-verify-token";
        private const string DefaultUpgradeUrl = "https://playprobe.io/account/billing";

        internal enum Outcome
        {
            /// <summary>Token is real and every checkable gate passes. Sessions will start.</summary>
            Ready,

            /// <summary>Token is real, but something would stop a session — see <see cref="Result.Problems"/>.</summary>
            Blocked,

            /// <summary>No test has this share token.</summary>
            InvalidToken,

            /// <summary>The check itself failed. Says nothing about the token.</summary>
            Unreachable,
        }

        internal sealed class Result
        {
            public Outcome Outcome;
            public string TestName = string.Empty;
            public string TestStatus = string.Empty;
            public string Plan = string.Empty;
            public bool IsPro;
            public bool UsesSdk;
            public string UpgradeUrl = DefaultUpgradeUrl;

            /// <summary>One human-readable line per reason a session would be refused.</summary>
            public List<string> Problems = new List<string>();

            /// <summary>True when the account needs upgrading — the one problem with a button.</summary>
            public bool NeedsUpgrade;
        }

        /// <summary>True while a check is in flight, so callers can disable their button.</summary>
        internal static bool IsChecking { get; private set; }

        /// <summary>
        /// Runs the check without blocking the editor. <paramref name="onComplete"/> is invoked on
        /// the main thread, exactly once, whatever the result — including failures, so a caller can
        /// always leave its "checking..." state.
        /// <para>
        /// A domain reload (any script recompile) mid-flight drops the poll loop and the callback
        /// never arrives. Callers should reset their own state in <c>OnEnable</c> rather than trust
        /// a pending check to survive.
        /// </para>
        /// </summary>
        internal static void Verify(string shareToken, Action<Result> onComplete)
        {
            if (onComplete == null)
            {
                return;
            }

            string token = shareToken != null ? shareToken.Trim() : string.Empty;

            if (token.Length == 0)
            {
                onComplete(new Result
                {
                    Outcome = Outcome.InvalidToken,
                    Problems = { "No share token to check. Copy it from the test's page in the PlayProbe dashboard." },
                });
                return;
            }

            if (IsChecking)
            {
                // Answer anyway rather than returning silently: a caller that got no callback would
                // sit on "Checking..." forever with no way back.
                onComplete(new Result
                {
                    Outcome = Outcome.Unreachable,
                    Problems = { "Another token check is already running. Try again in a moment." },
                });
                return;
            }

            string payloadJson = JsonUtility.ToJson(new VerifyRequest { share_token = token });
            string url = $"{PlayProbeRuntimeConfig.ApiEndpoint}{EndpointName}";
            UnityWebRequest request = PlayProbeHttp.CreatePostRequest(url, payloadJson);

            IsChecking = true;
            request.SendWebRequest();

            // EditorApplication.update rather than the async operation's completed callback: this
            // has to keep ticking while the setup window is the only thing on screen and nothing
            // else is driving the editor's player loop.
            void Poll()
            {
                if (!request.isDone)
                {
                    return;
                }

                EditorApplication.update -= Poll;
                IsChecking = false;

                Result result = Interpret(request);
                request.Dispose();
                onComplete(result);
            }

            EditorApplication.update += Poll;
        }

        private static Result Interpret(UnityWebRequest request)
        {
            if (request.result is UnityWebRequest.Result.ConnectionError)
            {
                return new Result
                {
                    Outcome = Outcome.Unreachable,
                    Problems = { $"Could not reach PlayProbe: {request.error}" },
                };
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

            if (request.responseCode != 200)
            {
                return new Result
                {
                    Outcome = Outcome.Unreachable,
                    Problems = { $"PlayProbe replied with status {request.responseCode}. Try again in a moment." },
                };
            }

            VerifyResponse response;
            try
            {
                response = JsonUtility.FromJson<VerifyResponse>(body);
            }
            catch (Exception exception)
            {
                return new Result
                {
                    Outcome = Outcome.Unreachable,
                    Problems = { $"Could not read PlayProbe's reply: {exception.Message}" },
                };
            }

            if (response == null || !response.valid)
            {
                return new Result
                {
                    Outcome = Outcome.InvalidToken,
                    Problems =
                    {
                        "No test has this share token. Check for a stray space, and copy it again from the "
                        + "test's page in the PlayProbe dashboard.",
                    },
                };
            }

            Result result = new Result
            {
                Outcome = response.ready ? Outcome.Ready : Outcome.Blocked,
                TestName = response.test_name ?? string.Empty,
                TestStatus = response.test_status ?? string.Empty,
                Plan = response.plan ?? string.Empty,
                IsPro = response.is_pro,
                UsesSdk = response.uses_sdk,
                UpgradeUrl = !string.IsNullOrWhiteSpace(response.upgrade_url) ? response.upgrade_url : DefaultUpgradeUrl,
            };

            if (response.problems != null)
            {
                foreach (VerifyProblem problem in response.problems)
                {
                    if (problem == null || string.IsNullOrWhiteSpace(problem.message))
                    {
                        continue;
                    }

                    result.Problems.Add(problem.message);

                    if (problem.code == "plan_required")
                    {
                        result.NeedsUpgrade = true;

                        if (!string.IsNullOrWhiteSpace(problem.action_url))
                        {
                            result.UpgradeUrl = problem.action_url;
                        }
                    }
                }
            }

            return result;
        }

        #region Wire types

        // JsonUtility needs concrete serializable types and matching field names; these mirror the
        // sdk-verify-token payloads exactly and exist nowhere else.

        [Serializable]
        private class VerifyRequest
        {
            public string share_token;
        }

        [Serializable]
        private class VerifyProblem
        {
            public string code;
            public string message;
            public string action_url;
        }

        [Serializable]
        private class VerifyResponse
        {
            public bool valid;
            public bool ready;
            public string test_name;
            public string test_status;
            public bool uses_sdk;
            public bool is_pro;
            public string plan;
            public int max_testers;
            public string upgrade_url;
            public VerifyProblem[] problems;
        }

        #endregion
    }
}
