// Zedex Assistant floating chat — UI stage only.
// Messages are mock/demo; the server AI-agent integration (the real
// endpoint call) is the next stage and plugs in where sendMock() lives.
(function () {
    'use strict';

    var widget = document.getElementById('zedexChat');
    var launcher = document.getElementById('chatLauncher');
    var messages = document.getElementById('chatMessages');
    var form = document.getElementById('chatForm');
    var input = document.getElementById('chatInput');
    var sendBtn = document.getElementById('chatSend');
    var welcome = document.getElementById('chatWelcomeHint');

    var typingDelayMs = 900;

    var mockReplies = [
        'Got it — I can help you explore that. For this first stage I’m running on mock replies so you can review the look and feel before the AI backend is wired in.',
        'Here from the Zedex Assistant! Right now I’m in UI-demo mode. Once the server AI agent is connected, I’ll answer against your real products, stock, bills, and ledgers.',
        'Thanks for the message. This chat UI is fully interactive on the front end — sending a proper AI answer is exactly the next integration step.',
        'I’m Zedex Assistant. While the AI backend isn’t connected yet, I’m replying with sample text so you can review the chat design end to end.'
    ];

    function isOpen() {
        return widget ? widget.getAttribute('data-open') === 'true' : false;
    }

    function scrollToBottom() {
        if (messages) messages.scrollTop = messages.scrollHeight;
    }

    function addMessage(text, fromUser, meta) {
        if (!messages) return;
        var bubble = document.createElement('div');
        bubble.className = 'chat-msg-bubble ' + (fromUser ? 'user' : 'assistant');
        var label = document.createElement('span');
        label.className = 'chat-msg-text';
        label.textContent = text;
        var stamp = document.createElement('span');
        stamp.className = 'chat-msg-meta';
        stamp.textContent = meta || 'now';
        bubble.appendChild(label);
        bubble.appendChild(stamp);
        messages.appendChild(bubble);
        scrollToBottom();
    }

    function showTyping(visible) {
        if (!messages) return;
        var existing = messages.querySelector('.chat-typing');
        if (visible && !existing) {
            var el = document.createElement('div');
            el.className = 'chat-typing';
            el.setAttribute('role', 'status');
            el.setAttribute('aria-label', 'Zedex Assistant is typing');
            el.innerHTML = '<span></span><span></span><span></span>';
            messages.appendChild(el);
            scrollToBottom();
        } else if (!visible && existing) {
            existing.remove();
        }
    }

    function sendMock() {
        var text = input ? input.value.trim() : '';
        if (!text) return;
        addMessage(text, true);
        input.value = '';
        input.focus();
        if (sendBtn) sendBtn.disabled = true;
        showTyping(true);
        setTimeout(function () {
            showTyping(false);
            var reply = mockReplies[Math.floor(Math.random() * mockReplies.length)];
            addMessage(reply, false);
            if (sendBtn) sendBtn.disabled = false;
        }, typingDelayMs);
    }

    function toggleOpen(force) {
        if (!widget || !launcher) return;
        var open = force !== undefined ? force : !isOpen();
        widget.setAttribute('data-open', open ? 'true' : 'false');
        launcher.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open) {
            // Show the one-time welcome hint on first open.
            if (welcome) {
                welcome.classList.remove('d-none');
                welcome = null;
            }
            setTimeout(function () { if (input) input.focus(); }, 150);
            scrollToBottom();
        }
    }

    if (launcher) {
        launcher.addEventListener('click', function () { toggleOpen(); });
    }
    if (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            sendMock();
        });
    }
    if (input) {
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendMock();
            }
        });
    }

    var more = document.querySelector('.chat-more');
    if (more) {
        more.addEventListener('click', function () { toggleOpen(false); });
    }

    // Always start collapsed.
    if (widget) widget.setAttribute('data-open', 'false');
})();