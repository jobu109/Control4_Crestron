# Installer discovery and setup report

User requirement recorded 2026-11-15. Implement after the necessary device relationships and feedback contracts have been identified. This is a planned feature, not an existing gateway capability.

Provide a single SIMPL gateway input pulse to discover and export all supported integration setup information from the module itself. Installers must not need Composer Pro, Python, manual token entry, or knowledge of proxy/output identifiers to interpret the results. Existing raw inventory and research captures do not satisfy this requirement.

Produce a readable report grouped by room, with friendly device names and plain-language roles, alongside a formatted JSON version for tooling. Include supported lights, shades, audio/video endpoints, sources, and relevant relationships and capabilities as they are established. For each configurable module, present its exact parameter names and recommended values, with clear physical-driver versus proxy distinctions and shared-matrix output assignments.

For example, a Living Room section should identify the TV as the Watch audio endpoint and Audio Matrix Switch as the Listen audio endpoint, then show Audio module values Gateway_ID=1, Control4_Room_ID=13, Watch_Feedback_ID=682, Listen_Feedback_ID=340, Listen_Output_ID=4003. Describe output 4003 as the Living Room audio output, retaining the numeric value for entry into SIMPL.

Mappings must be discovered or supported by verified relationships, never generalized from this installation's hardcoded IDs. Mark unresolved, conflicting or manually confirmed relationships explicitly; show partial discovery and failures without presenting guesses as ready-to-use configuration. If static metadata cannot establish a relationship, offer a clearly described guided identification step rather than requiring users to interpret raw event logs.

Expose export progress/completion and file paths through gateway feedback. Include timestamp, completeness and verification status. Reports must omit credentials, tokens and sensitive media/account payloads. Use the existing shared gateway authentication and retain local certificate handling and 4-Series-only constraints.

Acceptance: an installer can pulse discovery from SIMPL, open the readable report, locate a room/device by name, and enter the listed module parameters without translating proxy IDs or matrix output numbers. Verify on a different room/output as well as the Living Room before calling automatic mapping complete.

Implementation update: CP10a provides the first readable TXT/JSON report and one SIMPL export pulse. It discovers named room Watch/Listen sources, lists inventory and shows configured parameter mappings. Automatic endpoint/output relationships, shade classification and multiroom guided identification remain unfinished; see SETUP_REPORT_TEST_PLAN.md. Do not mark the full installer acceptance requirement complete yet.
