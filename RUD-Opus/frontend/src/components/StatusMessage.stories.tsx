import type { Meta, StoryObj } from '@storybook/react-vite'
import { StatusMessage } from './StatusMessage'

const meta = { title: 'Components/StatusMessage', component: StatusMessage } satisfies Meta<typeof StatusMessage>
export default meta
type Story = StoryObj<typeof meta>

export const Empty: Story = { args: { message: null } }
export const SuccessConfirmed: Story = {
  args: { message: { kind: 'success', text: 'Ada Lovelace is confirmed for this workshop.' }, onDismiss: () => {} },
}
export const SuccessWaitlisted: Story = {
  args: { message: { kind: 'success', text: 'Alan Turing was added to the waitlist at position 2.' }, onDismiss: () => {} },
}
export const ScheduleConflict: Story = {
  args: { message: { kind: 'error', text: 'Ada Lovelace is already registered for an overlapping workshop.' }, onDismiss: () => {} },
}
export const Duplicate: Story = {
  args: { message: { kind: 'error', text: 'This participant is already registered for the workshop.' } },
}
