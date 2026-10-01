import type { Meta, StoryObj } from '@storybook/react-vite'
import { StatePanel } from './StatePanel'

const meta = { title: 'Components/StatePanel', component: StatePanel } satisfies Meta<typeof StatePanel>
export default meta
type Story = StoryObj<typeof meta>

export const Loading: Story = { args: { variant: 'loading' } }
export const Empty: Story = { args: { variant: 'empty', message: 'No workshops have been scheduled yet.' } }
export const Error: Story = {
  args: { variant: 'error', message: 'Could not reach the server. Check your connection and try again.', onRetry: () => {} },
}
export const ErrorWithoutRetry: Story = { args: { variant: 'error', message: 'Workshop not found.' } }
