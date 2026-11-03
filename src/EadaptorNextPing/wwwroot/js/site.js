/* eAdaptor Next Ping - vanilla JS, no build chain */
(function () {
    'use strict';

    const $ = (sel) => document.querySelector(sel);

    async function api(url, options) {
        const res = await fetch(url, options);
        const text = await res.text();
        let data = null;
        try { data = text ? JSON.parse(text) : null; } catch (e) { data = { error: text }; }
        if (!res.ok) throw new Error((data && data.error) || res.statusText || 'Request failed');
        return data;
    }

    const getJSON = (url) => api(url);
    const postJSON = (url, body) => api(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });

    let endpointTemplate = '';
    let lastAutoUrl = '';

    function toast(title, message, kind) {
        const host = $('#toasts');
        const el = document.createElement('div');
        el.className = 'toast ' + (kind || '');
        el.innerHTML = '<div class="toast-title">' + escapeHtml(title) + '</div>' +
            (message ? '<div class="toast-msg">' + escapeHtml(message) + '</div>' : '');
        host.appendChild(el);
        while (host.children.length > 4) host.removeChild(host.firstChild);
        setTimeout(() => el.remove(), 4500);
    }

    function escapeHtml(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function normalizeTokenEndpoint(url) {
        let t = (url || '').trim().replace(/\/+$/, '');
        if (!t) return t;
        if (/\/token$/i.test(t)) return t;
        if (/\/v2\.0$/i.test(t) || /\/v1\.0$/i.test(t)) {
            const idx = Math.max(t.toLowerCase().lastIndexOf('/v2.0'), t.toLowerCase().lastIndexOf('/v1.0'));
            return t.substring(0, idx) + '/oauth2' + t.substring(idx) + '/token';
        }
        if (/\/oauth2\/v2\.0$/i.test(t) || /\/oauth2\/v1\.0$/i.test(t)) return t + '/token';
        if (/\/oauth2$/i.test(t)) return t + '/v2.0/token';
        return t;
    }

    function resolveTokenEndpoint(raw, clientId) {
        let t = normalizeTokenEndpoint(raw);
        const cid = (clientId || '').trim();
        if (cid) t = t.replace(/\{clientid\}/gi, cid);
        return t;
    }

    function defaultEndpointUrl(useAsync) {
        return 'https://{enterprisecode}{servercode}services.wisegrid.net/eAdaptorNext' + (useAsync ? '/Async' : '');
    }

    function resolveEndpoint(url) {
        let t = (url || '').trim();
        const ent = $('#enterpriseCodeBox').value.trim();
        const srv = $('#serverCodeBox').value.trim();
        if (ent) t = t.replace(/\{enterprisecode\}/gi, ent);
        if (srv) t = t.replace(/\{servercode\}/gi, srv);
        if ($('#modeSelect').value === 'async' && !/\/async$/i.test(t)) t = t.replace(/\/+$/, '') + '/Async';
        return t;
    }

    function updateEndpointHint() {
        const raw = $('#urlBox').value.trim();
        if (!raw) {
            $('#endpointHint').textContent = '';
            return;
        }
        const resolved = resolveEndpoint(raw);
        $('#endpointHint').textContent = raw.toLowerCase() !== resolved.toLowerCase()
            ? 'Resolved: ' + resolved
            : '';
    }

    function updateUrlPlaceholder() {
        const suffix = $('#modeSelect').value === 'async' ? '/Async' : '';
        $('#urlBox').placeholder = 'https://{Enterprise Code}{Server Code}services.wisegrid.net/eAdaptorNext' + suffix;
    }

    function renderEndpointBox() {
        const ent = $('#enterpriseCodeBox').value.trim();
        const srv = $('#serverCodeBox').value.trim();
        const useAsync = $('#modeSelect').value === 'async';
        if (!ent && !srv) {
            $('#urlBox').value = '';
            lastAutoUrl = '';
            return;
        }
        let base = (endpointTemplate || defaultEndpointUrl(useAsync)).replace(/\/async$/i, '');
        if (ent) base = base.split('{enterprisecode}').join(ent).split('{EnterpriseCode}').join(ent);
        if (srv) base = base.split('{servercode}').join(srv).split('{ServerCode}').join(srv);
        if (useAsync) base = base.replace(/\/+$/, '') + '/Async';
        $('#urlBox').value = base;
        lastAutoUrl = base;
    }

    function onCodesInput() {
        renderEndpointBox();
        updateEndpointHint();
    }

    function onEnterpriseCodeInput() {
        onCodesInput();
        if (!$('#csrModal').classList.contains('hidden')) {
            $('#enterpriseIdBox').value = $('#enterpriseCodeBox').value.trim();
            updateCnPreview();
        }
    }

    function onUrlInput() {
        const v = $('#urlBox').value.trim();
        if (v && v !== lastAutoUrl) endpointTemplate = v;
        updateEndpointHint();
    }

    function onModeChange() {
        const useAsync = $('#modeSelect').value === 'async';
        if (!endpointTemplate) endpointTemplate = defaultEndpointUrl(useAsync);
        endpointTemplate = endpointTemplate.replace(/\/async$/i, '');
        if (useAsync) endpointTemplate = endpointTemplate.replace(/\/+$/, '') + '/Async';
        updateUrlPlaceholder();
        renderEndpointBox();
        updateEndpointHint();
    }

    function collectSettings() {
        return {
            url: $('#urlBox').value.trim(),
            useAsync: $('#modeSelect').value === 'async',
            enterpriseCode: $('#enterpriseCodeBox').value.trim(),
            serverCode: $('#serverCodeBox').value.trim(),
            timeoutSeconds: parseInt($('#timeoutBox').value, 10) || 250,
            authMode: $('#authMode').value,
            basicUser: $('#basicUserBox').value,
            basicPassword: $('#basicPwdBox').value,
            tokenEndpoint: $('#tokenEndpointBox').value.trim(),
            autoFixTokenUrl: true,
            clientId: $('#clientIdBox').value.trim(),
            scope: $('#scopeBox').value.trim(),
            certSource: $('#certSource').value,
            certificatePath: $('#certPathBox').value.trim(),
            privateKeyPath: $('#keyPathBox').value.trim(),
            keyPassword: $('#keyPwdBox').value,
            pfxPath: $('#pfxPathBox').value.trim(),
            pfxPassword: $('#pfxPwdBox').value,
            requestBody: $('#bodyBox').value
        };
    }

    function fillSettings(s) {
        if (!s) return;
        endpointTemplate = s.url || defaultEndpointUrl(!!s.useAsync);
        $('#modeSelect').value = s.useAsync ? 'async' : 'sync';
        $('#enterpriseCodeBox').value = s.enterpriseCode || '';
        $('#serverCodeBox').value = s.serverCode || '';
        $('#timeoutBox').value = s.timeoutSeconds || 250;
        $('#authMode').value = s.authMode || 'OAuth';
        $('#basicUserBox').value = s.basicUser || '';
        $('#basicPwdBox').value = s.basicPassword || '';
        $('#tokenEndpointBox').value = s.tokenEndpoint || '';
        $('#clientIdBox').value = s.clientId || '';
        $('#scopeBox').value = s.scope || '';
        $('#certSource').value = s.certSource || 'PemPair';
        $('#certPathBox').value = s.certificatePath || '';
        $('#keyPathBox').value = s.privateKeyPath || '';
        $('#keyPwdBox').value = s.keyPassword || '';
        $('#pfxPathBox').value = s.pfxPath || '';
        $('#pfxPwdBox').value = s.pfxPassword || '';
        if (s.requestBody) $('#bodyBox').value = s.requestBody;
        applyAuthPanels();
        updateBodySize();
        updateTokenHint();
        updateUrlPlaceholder();
        renderEndpointBox();
        updateEndpointHint();
    }

    function applyAuthPanels() {
        const mode = $('#authMode').value;
        $('#oauthPanel').classList.toggle('hidden', mode !== 'OAuth');
        $('#basicPanel').classList.toggle('hidden', mode !== 'Basic');
        $('#nonePanel').hidden = mode !== 'None';
    }

    function wirePemLoader(btnSel, inputSel, targetSel) {
        const btn = $(btnSel);
        const input = $(inputSel);
        btn.addEventListener('click', () => input.click());
        input.addEventListener('change', () => {
            const file = input.files[0];
            if (!file) return;
            const reader = new FileReader();
            reader.onload = () => {
                $(targetSel).value = String(reader.result).trim();
            };
            reader.readAsText(file);
            input.value = '';
        });
    }

    function currentCn() {
        let id = $('#enterpriseIdBox').value.trim();
        if (!id) return '';
        if (!id.endsWith('-eAdaptorNext')) id += '-eAdaptorNext';
        return id;
    }

    function updateCnPreview() {
        const cn = currentCn();
        $('#csrCnPreview').textContent = cn
            ? 'CSR Common Name: CN=' + cn
            : 'CSR Common Name will be {EnterpriseID}-eAdaptorNext';
    }

    function buildOpensslCommand(prefix, cn, keySize, keyPassword) {
        const nodes = keyPassword ? '' : '-nodes ';
        const passout = keyPassword ? ' -passout pass:<your password>' : '';
        return 'openssl req -newkey rsa:' + keySize + ' ' + nodes +
            '-keyout ' + prefix + '.key -out ' + prefix + '.csr -subj "//CN=' + cn + '"' + passout;
    }

    function setDownloadLink(sel, text) {
        const el = $(sel);
        if (el.href && el.href.startsWith('blob:')) URL.revokeObjectURL(el.href);
        el.href = URL.createObjectURL(new Blob([text], { type: 'application/octet-stream' }));
    }

    function applyCertPanels() {
        const src = $('#certSource').value;
        $('#pemPanel').classList.toggle('hidden', src !== 'PemPair');
        $('#pfxPanel').classList.toggle('hidden', src !== 'Pfx');
    }

    function updateTokenHint() {
        const raw = $('#tokenEndpointBox').value.trim();
        if (!raw) {
            $('#tokenEndpointHint').textContent = '';
            return;
        }
        const resolved = resolveTokenEndpoint(raw, $('#clientIdBox').value);
        $('#tokenEndpointHint').textContent = raw.toLowerCase() !== resolved.toLowerCase()
            ? 'Token requests will use: ' + resolved
            : '';
    }

    function onClientIdInput() {
        const scopeBox = $('#scopeBox');
        const current = scopeBox.value.trim();
        const clientId = $('#clientIdBox').value.trim();
        if (current.length === 0 || /\{clientid\}/i.test(current))
            scopeBox.value = clientId ? clientId + '/.default' : '{clientid}/.default';
        updateTokenHint();
    }

    function updateBodySize() {
        const bytes = new TextEncoder().encode($('#bodyBox').value).length;
        $('#bodySize').textContent = bytes ? bytes.toLocaleString() + ' bytes' : 'empty';
    }

    function setAuthStatus(text, kind) {
        const el = $('#authStatus');
        el.textContent = text;
        el.style.color = kind === 'ok' ? 'var(--success)' : kind === 'err' ? 'var(--danger)' : '';
    }

    function renderResponse(record) {
        const http = $('#httpBadge');
        http.textContent = record.statusText;
        http.className = 'badge ' + (record.ok ? 'ok' : 'bad');
        http.classList.remove('hidden');

        $('#durText').textContent = record.durationText;

        const universal = $('#universalBadge');
        if (record.universalStatus) {
            universal.textContent = 'UniversalResponse: ' + record.universalStatus;
            universal.className = 'badge ' + (record.universalStatus.toUpperCase() === 'ERR' ? 'bad' : 'ok');
            universal.classList.remove('hidden');
        } else {
            universal.classList.add('hidden');
        }

        $('#processingLog').textContent = record.processingLog
            ? 'ProcessingLog: ' + record.processingLog : '';

        const headers = $('#respHeaders');
        if (record.headersText) {
            headers.textContent = record.headersText;
            headers.classList.remove('hidden');
        } else {
            headers.classList.add('hidden');
        }

        $('#respBody').textContent = record.body || (record.error ? 'Error: ' + record.error : '');
        $('#copyRespBtn').classList.remove('hidden');
    }

    async function init() {
        fillSettings(await getJSON('/api/settings').catch(() => null));
        updateUrlPlaceholder();

        $('#authMode').addEventListener('change', applyAuthPanels);
        $('#certSource').addEventListener('change', applyCertPanels);
        $('#tokenEndpointBox').addEventListener('input', updateTokenHint);
        $('#clientIdBox').addEventListener('input', onClientIdInput);
        $('#modeSelect').addEventListener('change', onModeChange);
        $('#urlBox').addEventListener('input', onUrlInput);
        $('#enterpriseCodeBox').addEventListener('input', onEnterpriseCodeInput);
        $('#serverCodeBox').addEventListener('input', onCodesInput);
        $('#bodyBox').addEventListener('input', updateBodySize);

        wirePemLoader('#loadCertBtn', '#certFileInput', '#certPathBox');
        wirePemLoader('#loadKeyBtn', '#keyFileInput', '#keyPathBox');

        $('#openCsrBtn').addEventListener('click', () => {
            $('#enterpriseIdBox').value = $('#enterpriseCodeBox').value.trim();
            $('#csrModal').classList.remove('hidden');
            updateCnPreview();
        });
        $('#closeCsrBtn').addEventListener('click', () => $('#csrModal').classList.add('hidden'));
        $('#csrModalBackdrop').addEventListener('click', () => $('#csrModal').classList.add('hidden'));
        $('#enterpriseIdBox').addEventListener('input', updateCnPreview);

        $('#generateCsrBtn').addEventListener('click', async () => {
            const cn = currentCn();
            if (!cn) {
                toast('EnterpriseID required', 'Enter the EnterpriseID', 'warn');
                return;
            }
            const prefix = ($('#csrPrefixBox').value.trim() ||
                $('#enterpriseIdBox').value.trim().toLowerCase() ||
                'eadaptor').replace(/[^\w.-]/g, '') || 'eadaptor';
            const keySize = parseInt($('#csrKeySize').value, 10) || 2048;
            const keyPassword = $('#csrKeyPwd').value;
            const btn = $('#generateCsrBtn');
            btn.disabled = true;
            $('#csrStatus').textContent = 'Generating...';
            try {
                const data = await postJSON('/api/csr', {
                    commonName: cn,
                    organization: '',
                    organizationalUnit: '',
                    country: '',
                    keySize: keySize,
                    keyPassword: keyPassword
                });
                $('#csrResult').classList.remove('hidden');
                $('#csrPemOut').textContent = data.csrPem;
                $('#keyPemOut').textContent = data.keyPem;
                $('#csrStatus').textContent = 'Key type: ' + data.keyAlgorithm;
                setDownloadLink('#downloadCsrBtn', data.csrPem);
                setDownloadLink('#downloadKeyBtn', data.keyPem);
                $('#downloadCsrBtn').setAttribute('download', prefix + '.csr');
                $('#downloadKeyBtn').setAttribute('download', prefix + '.key');
                $('#opensslCmdOut').textContent = buildOpensslCommand(prefix, cn, keySize, keyPassword);
                toast('CSR generated', 'Upload the .csr to CargoWise, then generate the certificate.', 'ok');
            } catch (e) {
                $('#csrStatus').textContent = '';
                toast('CSR failed', e.message, 'err');
            } finally {
                btn.disabled = false;
            }
        });

        $('#copyCsrBtn').addEventListener('click', () => navigator.clipboard.writeText($('#csrPemOut').textContent));
        $('#copyOpensslBtn').addEventListener('click', () => navigator.clipboard.writeText($('#opensslCmdOut').textContent));

        $('#sampleBtn').addEventListener('click', async () => {
            try {
                const data = await getJSON('/api/sample');
                $('#bodyBox').value = data.body;
                updateBodySize();
            } catch (e) { toast('Sample', e.message, 'err'); }
        });

        $('#loadFileBtn').addEventListener('click', () => $('#fileInput').click());
        $('#fileInput').addEventListener('change', () => {
            const file = $('#fileInput').files[0];
            if (!file) return;
            const reader = new FileReader();
            reader.onload = () => {
                $('#bodyBox').value = String(reader.result);
                updateBodySize();
            };
            reader.readAsText(file);
            $('#fileInput').value = '';
        });

        $('#formatBtn').addEventListener('click', async () => {
            try {
                const data = await postJSON('/api/format', { xml: $('#bodyBox').value });
                $('#bodyBox').value = data.formatted;
                updateBodySize();
            } catch (e) { toast('Invalid XML', e.message, 'warn'); }
        });

        $('#clearBtn').addEventListener('click', () => { $('#bodyBox').value = ''; updateBodySize(); });


        $('#testAuthBtn').addEventListener('click', async () => {
            const btn = $('#testAuthBtn');
            btn.disabled = true;
            setAuthStatus('Requesting token...', '');
            try {
                const data = await postJSON('/api/test', collectSettings());
                setAuthStatus('Authentication OK - token expires in ' + data.expiresIn, 'ok');
                toast('Authentication OK', 'Token expires in ' + data.expiresIn, 'ok');
            } catch (e) {
                setAuthStatus('Authentication failed: ' + e.message, 'err');
                toast('Authentication failed', e.message, 'err');
            } finally {
                btn.disabled = false;
            }
        });

        $('#sendBtn').addEventListener('click', async () => {
            const btn = $('#sendBtn');
            btn.disabled = true;
            $('#sendStatus').textContent = 'Sending...';
            try {
                const record = await postJSON('/api/send', { settings: collectSettings(), body: $('#bodyBox').value });
                renderResponse(record);
                $('#sendStatus').textContent = record.hasError
                    ? 'Failed after ' + record.durationText
                    : 'Completed in ' + record.durationText;
                if (record.hasError) {
                    toast('Send failed', record.error, 'err');
                } else if (String(record.universalStatus).toUpperCase() === 'ERR') {
                    toast('UniversalResponse ERR', record.processingLog || '', 'warn');
                } else {
                    toast('Request completed', 'HTTP ' + record.statusText + ' in ' + record.durationText, 'ok');
                }
            } catch (e) {
                $('#sendStatus').textContent = '';
                toast('Send failed', e.message, 'err');
            } finally {
                btn.disabled = false;
            }
        });

        $('#copyRespBtn').addEventListener('click', () => navigator.clipboard.writeText($('#respBody').textContent));
    }

    document.addEventListener('DOMContentLoaded', () => {
        init().catch(e => toast('Error', e.message, 'err'));
    });
})();
